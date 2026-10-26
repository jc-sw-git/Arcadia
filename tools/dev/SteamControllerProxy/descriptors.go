package main

import (
	"encoding/binary"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"os"
	"strconv"

	"github.com/Alia5/VIIPER/usb"
)

// descriptorFile is what the probe's describe mode read from the real puck (spike #239), as hex.
// The serial number string is not in it; the proxy reads it from the real device at start.
type descriptorFile struct {
	Device  string            `json:"device"`
	Config  string            `json:"config"`
	Strings map[string]string `json:"strings"`
	Reports map[string]string `json:"reports"`
	Speed   uint32            `json:"speed"`
}

// loadDescriptor turns the real device's raw descriptors into VIIPER's structured form, so the emulated device
// enumerates exactly like the real one.
func loadDescriptor(path string) (*usb.Descriptor, map[uint8][]byte, error) {
	raw, err := os.ReadFile(path)
	if err != nil {
		return nil, nil, err
	}
	var file descriptorFile
	if err := json.Unmarshal(raw, &file); err != nil {
		return nil, nil, fmt.Errorf("%s: %w", path, err)
	}
	dev, err := hex.DecodeString(file.Device)
	if err != nil || len(dev) != 18 {
		return nil, nil, fmt.Errorf("device descriptor: want 18 bytes of hex")
	}
	cfg, err := hex.DecodeString(file.Config)
	if err != nil || len(cfg) < 9 {
		return nil, nil, fmt.Errorf("configuration descriptor: want hex")
	}
	reports := map[uint8][]byte{}
	for key, value := range file.Reports {
		number, err := strconv.Atoi(key)
		if err != nil {
			return nil, nil, fmt.Errorf("report descriptor key %q", key)
		}
		bytes, err := hex.DecodeString(value)
		if err != nil {
			return nil, nil, fmt.Errorf("report descriptor %d: %w", number, err)
		}
		reports[uint8(number)] = bytes
	}

	speed := file.Speed
	if speed == 0 {
		speed = 2 // full speed, as a wireless HID dongle is
	}
	d := &usb.Descriptor{
		Device: usb.DeviceDescriptor{
			BcdUSB:             binary.LittleEndian.Uint16(dev[2:]),
			BDeviceClass:       dev[4],
			BDeviceSubClass:    dev[5],
			BDeviceProtocol:    dev[6],
			BMaxPacketSize0:    dev[7],
			IDVendor:           binary.LittleEndian.Uint16(dev[8:]),
			IDProduct:          binary.LittleEndian.Uint16(dev[10:]),
			BcdDevice:          binary.LittleEndian.Uint16(dev[12:]),
			IManufacturer:      dev[14],
			IProduct:           dev[15],
			ISerialNumber:      dev[16],
			BNumConfigurations: dev[17],
			Speed:              speed,
		},
		Strings: map[uint8]string{0: "Љ"},
	}
	for key, value := range file.Strings {
		index, err := strconv.Atoi(key)
		if err != nil {
			return nil, nil, fmt.Errorf("string key %q", key)
		}
		d.Strings[uint8(index)] = value
	}
	if err := parseConfiguration(d, cfg, reports); err != nil {
		return nil, nil, err
	}
	return d, reports, nil
}

// parseConfiguration walks the configuration descriptor: interfaces, their HID descriptors and endpoints, and
// keeps anything else as opaque class descriptors in place.
func parseConfiguration(d *usb.Descriptor, cfg []byte, reports map[uint8][]byte) error {
	d.Configuration = usb.ConfigurationDescriptor{
		BConfigurationValue: cfg[5],
		IConfiguration:      cfg[6],
		BMAttributes:        cfg[7],
		BMaxPower:           cfg[8],
	}
	var current *usb.InterfaceConfig
	for at := int(cfg[0]); at+1 < len(cfg); {
		length, kind := int(cfg[at]), cfg[at+1]
		if length < 2 || at+length > len(cfg) {
			return fmt.Errorf("configuration descriptor: bad length %d at %d", length, at)
		}
		body := cfg[at : at+length]
		switch {
		case kind == 0x0B && length >= 8:
			d.Associations = append(d.Associations, usb.InterfaceAssociationDescriptor{
				BFirstInterface: body[2], BInterfaceCount: body[3], BFunctionClass: body[4],
				BFunctionSubClass: body[5], BFunctionProtocol: body[6], IFunction: body[7],
			})
		case kind == 0x04 && length >= 9:
			d.Interfaces = append(d.Interfaces, usb.InterfaceConfig{Descriptor: usb.InterfaceDescriptor{
				BInterfaceNumber: body[2], BAlternateSetting: body[3], BNumEndpoints: body[4],
				BInterfaceClass: body[5], BInterfaceSubClass: body[6], BInterfaceProtocol: body[7], IInterface: body[8],
			}})
			current = &d.Interfaces[len(d.Interfaces)-1]
		case kind == 0x21 && current != nil && current.Descriptor.BInterfaceClass == 0x03 && length >= 6:
			hid := usb.HIDDescriptor{BcdHID: binary.LittleEndian.Uint16(body[2:]), BCountryCode: body[4]}
			for i := 0; i < int(body[5]) && 6+3*i+2 < length; i++ {
				// Length 0 lets VIIPER fill in the length of the report descriptor it serves.
				sub := usb.HIDSubDescriptor{Type: body[6+3*i], Length: binary.LittleEndian.Uint16(body[7+3*i:])}
				if sub.Type == usb.ReportDescType {
					sub.Length = 0
				}
				hid.Descriptors = append(hid.Descriptors, sub)
			}
			report, ok := reports[current.Descriptor.BInterfaceNumber]
			if !ok {
				return fmt.Errorf("no report descriptor for HID interface %d", current.Descriptor.BInterfaceNumber)
			}
			current.HID = &usb.HIDFunction{Descriptor: hid, ReportDescriptorBytes: report}
		case kind == 0x05 && current != nil && length >= 7:
			current.Endpoints = append(current.Endpoints, usb.EndpointDescriptor{
				BEndpointAddress: body[2], BMAttributes: body[3],
				WMaxPacketSize: binary.LittleEndian.Uint16(body[4:]), BInterval: body[6],
			})
		case current != nil && len(current.Endpoints) > 0:
			last := &current.Endpoints[len(current.Endpoints)-1]
			last.ClassDescriptors = append(last.ClassDescriptors, usb.ClassSpecificDescriptor{DescriptorType: kind, Payload: body[2:]})
		case current != nil:
			current.ClassDescriptors = append(current.ClassDescriptors, usb.ClassSpecificDescriptor{DescriptorType: kind, Payload: body[2:]})
		}
		at += length
	}
	if len(d.Interfaces) == 0 {
		return fmt.Errorf("configuration descriptor has no interfaces")
	}
	return nil
}

// reportSizes reads a HID report descriptor far enough to know, per report ID, how many bytes an input, output
// and feature report has (without the ID byte). ID 0 means the interface uses no report IDs.
type reportSizes struct {
	UsesIDs bool
	Input   map[uint8]int
	Output  map[uint8]int
	Feature map[uint8]int
}

func parseReportSizes(desc []byte) reportSizes {
	sizes := reportSizes{Input: map[uint8]int{}, Output: map[uint8]int{}, Feature: map[uint8]int{}}
	bits := map[string]map[uint8]int{"in": {}, "out": {}, "feat": {}}
	type globals struct {
		size, count uint32
		id          uint8
	}
	var g globals
	var stack []globals
	for at := 0; at < len(desc); {
		prefix := desc[at]
		if prefix == 0xFE { // long item
			if at+1 >= len(desc) {
				break
			}
			at += 3 + int(desc[at+1])
			continue
		}
		n := int(prefix & 0x03)
		if n == 3 {
			n = 4
		}
		if at+1+n > len(desc) {
			break
		}
		var value uint32
		for i := 0; i < n; i++ {
			value |= uint32(desc[at+1+i]) << (8 * i)
		}
		switch prefix & 0xFC {
		case 0x74: // Report Size
			g.size = value
		case 0x94: // Report Count
			g.count = value
		case 0x84: // Report ID
			g.id = uint8(value)
			sizes.UsesIDs = true
		case 0xA4: // Push
			stack = append(stack, g)
		case 0xB4: // Pop
			if len(stack) > 0 {
				g = stack[len(stack)-1]
				stack = stack[:len(stack)-1]
			}
		case 0x80:
			bits["in"][g.id] += int(g.size * g.count)
		case 0x90:
			bits["out"][g.id] += int(g.size * g.count)
		case 0xB0:
			bits["feat"][g.id] += int(g.size * g.count)
		}
		at += 1 + n
	}
	for id, b := range bits["in"] {
		sizes.Input[id] = (b + 7) / 8
	}
	for id, b := range bits["out"] {
		sizes.Output[id] = (b + 7) / 8
	}
	for id, b := range bits["feat"] {
		sizes.Feature[id] = (b + 7) / 8
	}
	return sizes
}
