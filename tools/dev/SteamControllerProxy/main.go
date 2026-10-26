//go:build windows

// Command steamproxy239 is the throwaway proxy of spike #239: it shows Steam an emulated copy of the real Steam
// Controller puck through VIIPER and usbip-win2 and passes every report between the two, so Steam keeps its layouts,
// gyro and trackpads while the real puck is hidden from it by HidHide. Built inside a VIIPER checkout (it uses
// VIIPER's internal USB server); not part of Arcadia.
//
//	steamproxy239 -descriptors descriptors.json -log proxy.log [-seconds 900] [-stopfile stop.txt] [-watch]
//	              [-parent pid -restore Restore-HidHide-239.ps1] [-allow A3,DC]
//
// -watch only reads the real device beside Steam and logs when its buttons change. Button changes always go to
// <log>.buttons with their QPC time. With -parent the proxy ends when that process (the check script) is gone and
// then runs the -restore script, so a closed window never leaves the puck hidden from Steam. -allow adds message
// types (hex) to the ones forwarded. The extra hop is measured all the time: the proxy reads its own emulated device
// like Steam does and logs how long each real report took to arrive there.
package main

import (
	"context"
	"flag"
	"fmt"
	"log/slog"
	"os"
	"os/exec"
	"os/signal"
	"strconv"
	"strings"
	"sync"
	"time"
	"unsafe"

	"github.com/Alia5/VIIPER/device"
	"github.com/Alia5/VIIPER/internal/server/api"
	usbserver "github.com/Alia5/VIIPER/internal/server/usb"
	"github.com/Alia5/VIIPER/virtualbus"
	"golang.org/x/sys/windows"
)

var (
	qpcFrequency int64
	kernel32     = windows.NewLazySystemDLL("kernel32.dll")
	procQPC      = kernel32.NewProc("QueryPerformanceCounter")
	procQPF      = kernel32.NewProc("QueryPerformanceFrequency")
)

// qpcNow is the same clock .NET's Stopwatch.GetTimestamp reads, so step 3 can line up both processes' times.
func qpcNow() int64 {
	var now int64
	procQPC.Call(uintptr(unsafe.Pointer(&now)))
	return now
}

func main() {
	descriptors := flag.String("descriptors", "descriptors.json", "the real puck's descriptors (from the probe's describe mode)")
	logPath := flag.String("log", "proxy.log", "log file")
	seconds := flag.Int("seconds", 900, "stop after this many seconds")
	stopFile := flag.String("stopfile", "", "stop as soon as this file exists")
	vidPid := flag.String("device", "vid_28de&pid_1304", "vendor and product id of the real device, as in its HID path")
	watch := flag.Bool("watch", false, "only read the real device and log its button changes, emulate nothing")
	parent := flag.Int("parent", 0, "end when this process is gone")
	restore := flag.String("restore", "", "PowerShell script to run when the parent process is gone")
	allow := flag.String("allow", "A3", "extra message types (hex, comma-separated) to forward")
	flag.Parse()

	file, err := os.Create(*logPath)
	if err != nil {
		os.Exit(2)
	}
	defer file.Close()
	log := slog.New(slog.NewTextHandler(file, &slog.HandlerOptions{Level: slog.LevelInfo}))
	slog.SetDefault(log)
	procQPF.Call(uintptr(unsafe.Pointer(&qpcFrequency)))
	log.Info("start", "qpcFrequency", qpcFrequency)

	for _, text := range strings.Split(*allow, ",") {
		if value, err := strconv.ParseUint(strings.TrimSpace(text), 16, 8); err == nil {
			allowedMessages[byte(value)] = "allowed by -allow"
		}
	}
	stop := stopWhen{duration: time.Duration(*seconds) * time.Second, file: *stopFile, parent: openParent(*parent)}
	err = run(log, *logPath+".buttons", *descriptors, *vidPid, *watch, &stop)
	if err != nil {
		log.Error("stopped", "error", err)
	}
	// Only now, with the emulated device gone, may the real one come back to Steam.
	if stop.parentGone && *restore != "" {
		out, restoreErr := exec.Command("powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", *restore, "-Quiet").CombinedOutput()
		log.Info("restore after the check script was closed", "error", restoreErr, "output", string(out))
	}
	if err != nil {
		os.Exit(1)
	}
	log.Info("done")
}

type stopWhen struct {
	duration   time.Duration
	file       string
	parent     windows.Handle
	parentGone bool
}

func openParent(pid int) windows.Handle {
	if pid == 0 {
		return 0
	}
	handle, err := windows.OpenProcess(windows.SYNCHRONIZE, false, uint32(pid))
	if err != nil {
		return 0
	}
	return handle
}

func run(log *slog.Logger, buttonsPath, descriptorPath, vidPid string, watch bool, stop *stopWhen) error {
	desc, reports, err := loadDescriptor(descriptorPath)
	if err != nil {
		return err
	}
	collections, err := findCollections(vidPid)
	if err != nil {
		return err
	}
	if len(collections) == 0 {
		return fmt.Errorf("real device %s not found (is this program on HidHide's allow list?)", vidPid)
	}
	serial := ""
	for _, list := range collections {
		for _, c := range list {
			if err := c.open(); err != nil {
				log.Warn("collection not opened", "path", c.Path, "error", err)
				continue
			}
			defer c.close()
			log.Info("collection", "interface", c.Interface, "path", c.Path, "usagePage", fmt.Sprintf("0x%04X", c.Caps.UsagePage),
				"usage", fmt.Sprintf("0x%04X", c.Caps.Usage), "input", c.Caps.InputLength, "output", c.Caps.OutputLength, "feature", c.Caps.FeatureLength)
			if serial == "" {
				serial = c.serialNumber()
			}
		}
	}
	if desc.Device.ISerialNumber != 0 {
		desc.Strings[desc.Device.ISerialNumber] = serial
		log.Info("serial number copied from the real device", "length", len(serial))
	}

	buttonsFile, err := os.Create(buttonsPath)
	if err != nil {
		return err
	}
	defer buttonsFile.Close()
	stats := newStats(log, slog.New(slog.NewTextHandler(buttonsFile, nil)))
	p := newProxy(desc, reports, collections, stats, log)

	ctx, cancel := signal.NotifyContext(context.Background(), os.Interrupt)
	defer cancel()
	var wg sync.WaitGroup
	p.startReaders(ctx, &wg)
	go stats.run(ctx)
	if watch {
		log.Info("watching the real device only")
		return wait(ctx, log, stop, nil)
	}

	server := usbserver.New(usbserver.ServerConfig{Addr: "127.0.0.1:3241", ConnectionTimeout: 30 * time.Second, BusCleanupTimeout: 5 * time.Second}, log, nil)
	served := make(chan error, 1)
	go func() { served <- server.ListenAndServe() }()
	select {
	case <-server.Ready():
	case err := <-served:
		return fmt.Errorf("usbip server: %w (does another VIIPER or SISR run?)", err)
	}
	defer server.Close()

	bus, err := virtualbus.NewWithBusID(server.NextFreeBusID())
	if err != nil {
		return err
	}
	if err := server.AddBus(bus); err != nil {
		return err
	}
	defer server.RemoveBus(bus.BusID())
	devCtx, err := bus.Add(p)
	if err != nil {
		return err
	}
	meta := device.GetDeviceMeta(devCtx)
	if meta == nil {
		return fmt.Errorf("no export metadata for the emulated device")
	}
	if err := api.AttachLocalhostClient(ctx, meta, server.GetListenPort(), true, log); err != nil {
		return fmt.Errorf("attach through usbip-win2: %w", err)
	}
	log.Info("emulated device attached", "bus", meta.BusID, "device", meta.DevID)
	go measureHop(ctx, log, vidPid, collections, p.ifaces, stats)
	return wait(ctx, log, stop, served)
}

// measureHop reads the emulated device's vendor collections, as Steam does, and matches every report with the moment
// the real device delivered it: that is the whole extra hop (proxy, usbip-win2, Windows' HID stack once more).
func measureHop(ctx context.Context, log *slog.Logger, vidPid string, real map[uint8][]*collection, ifaces map[uint8]*proxyInterface, stats *stats) {
	time.Sleep(3 * time.Second)
	known := map[string]bool{}
	for _, list := range real {
		for _, c := range list {
			known[strings.ToLower(c.Path)] = true
		}
	}
	all, err := findCollections(vidPid)
	if err != nil {
		log.Warn("hop measurement: list failed", "error", err)
		return
	}
	for _, list := range all {
		for _, c := range list {
			if known[strings.ToLower(c.Path)] {
				continue
			}
			pi := ifaces[c.Interface]
			if err := c.open(); err != nil || pi == nil || c.Caps.UsagePage < 0xFF00 || c.Caps.InputLength == 0 {
				c.close()
				continue
			}
			log.Info("hop measurement reads", "path", c.Path)
			go func(c *collection, sizes reportSizes) {
				defer c.close()
				buffer := make([]byte, c.Caps.InputLength)
				for ctx.Err() == nil {
					n, err := c.read(buffer)
					if err != nil {
						log.Warn("hop measurement read stopped", "path", c.Path, "error", err)
						return
					}
					at := qpcNow()
					stats.arrived(fromWindows(buffer[:n], sizes.UsesIDs, sizes.Input), at)
				}
			}(c, pi.sizes)
		}
	}
}

func wait(ctx context.Context, log *slog.Logger, stop *stopWhen, served <-chan error) error {
	deadline := time.After(stop.duration)
	ticker := time.NewTicker(200 * time.Millisecond)
	defer ticker.Stop()
	for {
		select {
		case <-ctx.Done():
			log.Info("interrupted")
			return nil
		case <-deadline:
			log.Info("time is up")
			return nil
		case err := <-served:
			return fmt.Errorf("usbip server ended: %w", err)
		case <-ticker.C:
			if stop.parent != 0 {
				if event, _ := windows.WaitForSingleObject(stop.parent, 0); event == windows.WAIT_OBJECT_0 {
					log.Warn("the check script is gone")
					stop.parentGone = true
					return nil
				}
			}
			if stop.file != "" {
				if _, err := os.Stat(stop.file); err == nil {
					log.Info("stop file found")
					return nil
				}
			}
		}
	}
}
