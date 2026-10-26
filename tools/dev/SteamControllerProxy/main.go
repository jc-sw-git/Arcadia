//go:build windows

// Command steamproxy239 is the throwaway proxy of spike #239: it shows Steam an emulated copy of the real Steam
// Controller puck through VIIPER and usbip-win2 and passes every report between the two, so Steam keeps its layouts,
// gyro and trackpads while the real puck is hidden from it by HidHide. Built inside a VIIPER checkout (it uses
// VIIPER's internal USB server); not part of Arcadia.
//
//	steamproxy239 -descriptors descriptors.json -log proxy.log [-seconds 900] [-stopfile stop.txt] [-watch]
//
// -watch only reads the real device beside Steam and logs when its buttons change (the latency baseline of step 3).
// Button changes always go to <log>.buttons with their QPC time.
package main

import (
	"context"
	"flag"
	"fmt"
	"log/slog"
	"os"
	"os/signal"
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

	stop := stopWhen{duration: time.Duration(*seconds) * time.Second, file: *stopFile}
	if err := run(log, *logPath+".buttons", *descriptors, *vidPid, *watch, stop); err != nil {
		log.Error("stopped", "error", err)
		os.Exit(1)
	}
	log.Info("done")
}

type stopWhen struct {
	duration time.Duration
	file     string
}

func run(log *slog.Logger, buttonsPath, descriptorPath, vidPid string, watch bool, stop stopWhen) error {
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
	return wait(ctx, log, stop, served)
}

func wait(ctx context.Context, log *slog.Logger, stop stopWhen, served <-chan error) error {
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
			if stop.file != "" {
				if _, err := os.Stat(stop.file); err == nil {
					log.Info("stop file found")
					return nil
				}
			}
		}
	}
}
