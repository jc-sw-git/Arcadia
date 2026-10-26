package main

import (
	"context"
	"fmt"
	"log/slog"
	"sort"
	"strings"
	"sync"
	"time"

	"github.com/Alia5/VIIPER/usb"
	"github.com/Alia5/VIIPER/usbip"
)

// proxy is the emulated copy of the real device: it enumerates with the real descriptors and passes every report
// 1:1 between Steam (through usbip-win2) and the real device, except commands that could change the controller
// for good (firmware, serial, pairing records), which it swallows and logs.
type proxy struct {
	desc   *usb.Descriptor
	ifaces map[uint8]*proxyInterface
	epIn   map[uint32]*proxyInterface
	epOut  map[uint32]*proxyInterface
	stats  *stats
	log    *slog.Logger
}

type proxyInterface struct {
	number      uint8
	sizes       reportSizes
	vendor      bool
	collections []*collection
	in          chan inReport
}

type inReport struct {
	data []byte
	at   int64 // QPC ticks when the real device's report arrived
}

// Message types (first payload byte of a feature report) the proxy forwards to a Valve controller. Taken from SDL's
// controller_constants.h; everything else, firmware and bootloader commands included, is swallowed.
var allowedMessages = map[byte]string{
	0x80: "SET_DIGITAL_MAPPINGS", 0x81: "CLEAR_DIGITAL_MAPPINGS", 0x82: "GET_DIGITAL_MAPPINGS", 0x83: "GET_ATTRIBUTES_VALUES",
	0x84: "GET_ATTRIBUTE_LABEL", 0x85: "SET_DEFAULT_DIGITAL_MAPPINGS", 0x87: "SET_SETTINGS_VALUES", 0x88: "CLEAR_SETTINGS_VALUES",
	0x89: "GET_SETTINGS_VALUES", 0x8A: "GET_SETTING_LABEL", 0x8B: "GET_SETTINGS_MAXS", 0x8C: "GET_SETTINGS_DEFAULTS",
	0x8D: "SET_CONTROLLER_MODE", 0x8E: "LOAD_DEFAULT_SETTINGS", 0x8F: "TRIGGER_HAPTIC_PULSE", 0x9F: "TURN_OFF_CONTROLLER",
	0xA1: "GET_DEVICE_INFO", 0xA7: "CALIBRATE_TRACKPADS", 0xAA: "GET_TRACKPAD_CALIBRATION", 0xAB: "GET_TRACKPAD_FACTORY_CALIBRATION",
	0xAC: "GET_TRACKPAD_RAW_DATA", 0xAD: "ENABLE_PAIRING", 0xAE: "GET_STRING_ATTRIBUTE", 0xB1: "SET_DONGLE_SETTING",
	0xB2: "DONGLE_DISCONNECT_DEVICE", 0xB3: "DONGLE_COMMIT_DEVICE", 0xB4: "DONGLE_GET_WIRELESS_STATE", 0xB5: "CALIBRATE_GYRO",
	0xB6: "PLAY_AUDIO", 0xBA: "GET_CHIPID", 0xBF: "CALIBRATE_JOYSTICK", 0xC0: "CALIBRATE_ANALOG_TRIGGERS",
	0xC1: "SET_AUDIO_MAPPING", 0xC2: "CHECK_GYRO_FW_LOAD", 0xC3: "CALIBRATE_ANALOG", 0xC4: "DONGLE_GET_CONNECTED_SLOTS",
	0xCE: "RESET_IMU", 0xEA: "TRIGGER_HAPTIC_CMD", 0xEB: "TRIGGER_RUMBLE_CMD",
}

func newProxy(desc *usb.Descriptor, reports map[uint8][]byte, collections map[uint8][]*collection, stats *stats, log *slog.Logger) *proxy {
	p := &proxy{desc: desc, ifaces: map[uint8]*proxyInterface{}, epIn: map[uint32]*proxyInterface{}, epOut: map[uint32]*proxyInterface{}, stats: stats, log: log}
	for _, iface := range desc.Interfaces {
		number := iface.Descriptor.BInterfaceNumber
		pi := &proxyInterface{number: number, sizes: parseReportSizes(reports[number]), collections: collections[number], in: make(chan inReport, 64)}
		for _, c := range pi.collections {
			pi.vendor = pi.vendor || c.Caps.UsagePage >= 0xFF00
		}
		p.ifaces[number] = pi
		for _, ep := range iface.Endpoints {
			if ep.BMAttributes&0x03 != 0x03 {
				continue
			}
			if ep.BEndpointAddress&0x80 != 0 {
				p.epIn[uint32(ep.BEndpointAddress&0x0F)] = pi
			} else {
				p.epOut[uint32(ep.BEndpointAddress&0x0F)] = pi
			}
		}
		log.Info("interface", "number", number, "vendor", pi.vendor, "collections", len(pi.collections), "reportIDs", pi.sizes.UsesIDs,
			"input", pi.sizes.Input, "output", pi.sizes.Output, "feature", pi.sizes.Feature)
	}
	return p
}

// NoInRepeat asks VIIPER's server (patched for this spike) not to resend the last input report when no new one
// came within bInterval: the real device sends its own reports, and some of them are events, not state.
func (p *proxy) NoInRepeat() bool { return true }

func (p *proxy) GetDescriptor() *usb.Descriptor { return p.desc }

func (p *proxy) GetDeviceSpecificArgs() map[string]any { return map[string]any{} }

// startReaders reads every real collection that delivers input and queues its reports for the emulated endpoint.
func (p *proxy) startReaders(ctx context.Context, wg *sync.WaitGroup) {
	for _, pi := range p.ifaces {
		for _, c := range pi.collections {
			if c.Caps.InputLength == 0 {
				continue
			}
			wg.Add(1)
			go func(pi *proxyInterface, c *collection) {
				defer wg.Done()
				buffer := make([]byte, c.Caps.InputLength)
				for ctx.Err() == nil {
					n, err := c.read(buffer)
					if err != nil {
						p.log.Warn("read stopped", "interface", pi.number, "error", err)
						return
					}
					at := qpcNow()
					data := fromWindows(buffer[:n], pi.sizes.UsesIDs, pi.sizes.Input)
					p.stats.input(pi.number, data, at, pi.vendor)
					select {
					case pi.in <- inReport{data: append([]byte(nil), data...), at: at}:
					default:
						<-pi.in // Steam is not polling fast enough: drop the oldest, keep the newest
						pi.in <- inReport{data: append([]byte(nil), data...), at: at}
						p.stats.dropped(pi.number)
					}
				}
			}(pi, c)
		}
	}
}

func (p *proxy) HandleTransfer(ctx context.Context, ep uint32, dir uint32, out []byte) []byte {
	if dir == usbip.DirIn {
		pi := p.epIn[ep]
		if pi == nil {
			<-ctx.Done()
			return nil
		}
		select {
		case report := <-pi.in:
			p.stats.delivered(pi.number, report.at)
			return report.data
		case <-ctx.Done():
			return nil
		}
	}
	pi := p.epOut[ep]
	if pi == nil || len(out) == 0 || !p.allowed(pi, out, "interrupt OUT", true) {
		return nil
	}
	for _, c := range pi.collections {
		if c.Caps.OutputLength == 0 {
			continue
		}
		if err := c.write(toWindows(out, pi.sizes.UsesIDs, int(c.Caps.OutputLength))); err != nil {
			p.log.Warn("output write failed", "interface", pi.number, "error", err)
		}
		p.stats.count(fmt.Sprintf("OUT iface %d", pi.number))
		return nil
	}
	return nil
}

// HandleControl forwards HID GET_REPORT and SET_REPORT to the real device; everything else (SET_IDLE and the
// like) is left to VIIPER's defaults.
func (p *proxy) HandleControl(bmRequestType, bRequest uint8, wValue, wIndex, wLength uint16, data []byte) ([]byte, bool) {
	const getReport, setReport = 0x01, 0x09
	pi := p.ifaces[uint8(wIndex)]
	if pi == nil || (bmRequestType != 0xA1 && bmRequestType != 0x21) || (bRequest != getReport && bRequest != setReport) {
		return nil, false
	}
	kind, id := byte(wValue>>8), byte(wValue)
	name := map[byte]string{1: "input", 2: "output", 3: "feature"}[kind]
	if bRequest == getReport {
		for _, c := range pi.collections {
			length := c.Caps.FeatureLength
			call := c.getFeature
			if kind == 1 {
				length, call = c.Caps.InputLength, c.getInput
			}
			if length == 0 {
				continue
			}
			buffer := make([]byte, length)
			buffer[0] = id
			if err := call(buffer); err != nil {
				continue
			}
			p.stats.count(fmt.Sprintf("GET_REPORT %s id %d iface %d", name, id, pi.number))
			return fromWindows(buffer, pi.sizes.UsesIDs, sizesFor(pi.sizes, kind)), true
		}
		p.stats.count(fmt.Sprintf("GET_REPORT %s id %d iface %d FAILED", name, id, pi.number))
		return nil, true
	}

	if !p.allowed(pi, data, "SET_REPORT "+name, kind == 2) {
		return nil, true
	}
	for _, c := range pi.collections {
		length, call := c.Caps.FeatureLength, c.setFeature
		if kind == 2 {
			length, call = c.Caps.OutputLength, c.setOutput
		}
		if length == 0 {
			continue
		}
		if err := call(toWindows(data, pi.sizes.UsesIDs, int(length))); err != nil {
			continue
		}
		return nil, true
	}
	p.stats.count(fmt.Sprintf("SET_REPORT %s id %d iface %d FAILED", name, id, pi.number))
	return nil, true
}

// allowed checks a report Steam sends to a Valve vendor interface against the message list. Only the message type
// and length are logged, never the payload (it can hold serial numbers). On the puck, messages go as feature report
// 1 or 2 with the type in the next byte (as SDL sends them), and as output reports whose id is the type (0x80-0x89).
func (p *proxy) allowed(pi *proxyInterface, data []byte, what string, output bool) bool {
	if !pi.vendor {
		return true
	}
	at := 0
	if pi.sizes.UsesIDs && !output {
		at = 1
	}
	if len(data) <= at {
		return true
	}
	message := data[at]
	if name, ok := allowedMessages[message]; ok {
		p.stats.count(fmt.Sprintf("%s iface %d %s (0x%02X)", what, pi.number, name, message))
		return true
	}
	p.stats.count(fmt.Sprintf("%s iface %d BLOCKED 0x%02X", what, pi.number, message))
	p.log.Warn("blocked message", "what", what, "interface", pi.number, "message", fmt.Sprintf("0x%02X", message), "length", len(data))
	return false
}

func sizesFor(s reportSizes, kind byte) map[uint8]int {
	switch kind {
	case 1:
		return s.Input
	case 2:
		return s.Output
	}
	return s.Feature
}

// fromWindows turns a buffer from Windows' HID API (always led by the report id, 0 without ids, and padded to the
// collection's longest report) into what the device sends over USB.
func fromWindows(buffer []byte, usesIDs bool, sizes map[uint8]int) []byte {
	if len(buffer) == 0 {
		return buffer
	}
	if !usesIDs {
		size, ok := sizes[0]
		if !ok || size > len(buffer)-1 {
			size = len(buffer) - 1
		}
		return buffer[1 : 1+size]
	}
	size, ok := sizes[buffer[0]]
	if !ok || size > len(buffer)-1 {
		size = len(buffer) - 1
	}
	return buffer[:1+size]
}

// toWindows is the reverse: USB data (led by the report id only if the interface uses ids) into a Windows buffer.
func toWindows(data []byte, usesIDs bool, length int) []byte {
	buffer := make([]byte, max(length, len(data)+1))
	if usesIDs {
		copy(buffer, data)
		return buffer[:max(length, len(data))]
	}
	copy(buffer[1:], data)
	return buffer
}

// stats collects counts and the proxy's own delay (real report read → handed to usbip) and logs them every 10 s.
type stats struct {
	mu        sync.Mutex
	counts    map[string]int
	delaysUs  []float64
	lastState map[uint8]string
	log       *slog.Logger
	buttons   *slog.Logger
}

func newStats(log, buttons *slog.Logger) *stats {
	return &stats{counts: map[string]int{}, lastState: map[uint8]string{}, log: log, buttons: buttons}
}

func (s *stats) count(key string) {
	s.mu.Lock()
	s.counts[key]++
	s.mu.Unlock()
}

func (s *stats) dropped(iface uint8) { s.count(fmt.Sprintf("IN iface %d dropped", iface)) }

// input logs, for step 3 (latency), the QPC time at which the real controller's buttons changed. Triton state
// reports (0x42, 0x45, 0x47) carry a sequence byte, then the 32-bit button mask (SDL's controller_structs.h).
func (s *stats) input(iface uint8, data []byte, at int64, vendor bool) {
	s.count(fmt.Sprintf("IN iface %d", iface))
	if !vendor || len(data) < 6 || (data[0] != 0x42 && data[0] != 0x45 && data[0] != 0x47) {
		return
	}
	state := fmt.Sprintf("%X", data[2:6])
	s.mu.Lock()
	changed := s.lastState[iface] != state
	s.lastState[iface] = state
	s.mu.Unlock()
	if changed {
		s.buttons.Info("buttons", "qpc", at, "iface", iface, "mask", state)
	}
}

func (s *stats) delivered(iface uint8, at int64) {
	delay := float64(qpcNow()-at) * 1e6 / float64(qpcFrequency)
	s.mu.Lock()
	s.delaysUs = append(s.delaysUs, delay)
	s.mu.Unlock()
}

func (s *stats) report() {
	s.mu.Lock()
	counts, delays := s.counts, s.delaysUs
	s.counts, s.delaysUs = map[string]int{}, nil
	s.mu.Unlock()
	keys := make([]string, 0, len(counts))
	for k := range counts {
		keys = append(keys, k)
	}
	sort.Strings(keys)
	var b strings.Builder
	for _, k := range keys {
		fmt.Fprintf(&b, "%s=%d; ", k, counts[k])
	}
	s.log.Info("last 10 s", "counts", b.String())
	if len(delays) > 0 {
		sort.Float64s(delays)
		s.log.Info("proxy delay read->usbip (us)", "n", len(delays), "p50", delays[len(delays)/2],
			"p95", delays[len(delays)*95/100], "max", delays[len(delays)-1])
	}
}

func (s *stats) run(ctx context.Context) {
	ticker := time.NewTicker(10 * time.Second)
	defer ticker.Stop()
	for {
		select {
		case <-ctx.Done():
			s.report()
			return
		case <-ticker.C:
			s.report()
		}
	}
}
