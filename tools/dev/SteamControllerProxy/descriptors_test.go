package main

import (
	"bytes"
	"encoding/hex"
	"encoding/json"
	"os"
	"testing"

	"github.com/Alia5/VIIPER/usb"
)

// A report descriptor with ids: input 1 (8 bytes), feature 1 (64 bytes); another without ids: input 0 (3 bytes).
func TestParseReportSizes(t *testing.T) {
	withIDs := []byte{0x06, 0x00, 0xFF, 0x09, 0x01, 0xA1, 0x01, 0x85, 0x01, 0x75, 0x08, 0x95, 0x08, 0x81, 0x02,
		0x95, 0x40, 0xB1, 0x02, 0xC0}
	s := parseReportSizes(withIDs)
	if !s.UsesIDs || s.Input[1] != 8 || s.Feature[1] != 64 {
		t.Fatalf("with ids: %+v", s)
	}
	mouse := []byte{0x05, 0x01, 0x09, 0x02, 0xA1, 0x01, 0x05, 0x09, 0x19, 0x01, 0x29, 0x03, 0x15, 0x00, 0x25, 0x01,
		0x95, 0x03, 0x75, 0x01, 0x81, 0x02, 0x95, 0x01, 0x75, 0x05, 0x81, 0x03, 0x05, 0x01, 0x09, 0x30, 0x09, 0x31,
		0x15, 0x81, 0x25, 0x7F, 0x75, 0x08, 0x95, 0x02, 0x81, 0x06, 0xC0}
	s = parseReportSizes(mouse)
	if s.UsesIDs || s.Input[0] != 3 {
		t.Fatalf("mouse: %+v", s)
	}
}

func TestWindowsBuffers(t *testing.T) {
	sizes := map[uint8]int{1: 3}
	if got := fromWindows([]byte{1, 9, 8, 7, 0, 0}, true, sizes); len(got) != 4 || got[0] != 1 {
		t.Fatalf("with ids: %v", got)
	}
	if got := fromWindows([]byte{0, 9, 8, 7, 0}, false, map[uint8]int{0: 3}); len(got) != 3 || got[0] != 9 {
		t.Fatalf("without ids: %v", got)
	}
	if got := toWindows([]byte{9, 8}, false, 5); len(got) != 5 || got[0] != 0 || got[1] != 9 {
		t.Fatalf("to windows: %v", got)
	}
}

func TestParseConfiguration(t *testing.T) {
	// config header, HID interface 0 with HID descriptor and one interrupt IN endpoint
	cfg := []byte{9, 2, 34, 0, 1, 1, 0, 0xA0, 50,
		9, 4, 0, 0, 1, 3, 0, 0, 0,
		9, 0x21, 0x11, 0x01, 0, 1, 0x22, 20, 0,
		7, 5, 0x81, 3, 64, 0, 1}
	d, _, err := loadDescriptorFromBytes(cfg)
	if err != nil {
		t.Fatal(err)
	}
	iface := d.Interfaces[0]
	if iface.HID == nil || len(iface.Endpoints) != 1 || iface.Endpoints[0].BEndpointAddress != 0x81 || iface.Endpoints[0].BInterval != 1 {
		t.Fatalf("%+v", iface)
	}
}

func loadDescriptorFromBytes(cfg []byte) (*usb.Descriptor, map[uint8][]byte, error) {
	reports := map[uint8][]byte{0: {0x06, 0x00, 0xFF, 0x09, 0x01, 0xA1, 0x01, 0xC0}}
	d := &usb.Descriptor{}
	return d, reports, parseConfiguration(d, cfg, reports)
}

// The real puck (owner's PC, spike #239): re-encoding the parsed configuration must give back the original bytes,
// apart from the report descriptor lengths (hidapi's reconstruction is not byte-identical to the device's own).
func TestPuckRoundTrip(t *testing.T) {
	d, reports, err := loadDescriptor("testdata/puck.json")
	if err != nil {
		t.Fatal(err)
	}
	raw, _ := os.ReadFile("testdata/puck.json")
	var file descriptorFile
	_ = json.Unmarshal(raw, &file)
	original, _ := hex.DecodeString(file.Config)

	var b bytes.Buffer
	usb.ConfigHeader{WTotalLength: uint16(len(original)), BNumInterfaces: d.NumInterfaces(), BConfigurationValue: d.Configuration.BConfigurationValue,
		IConfiguration: d.Configuration.IConfiguration, BMAttributes: d.Configuration.BMAttributes, BMaxPower: d.Configuration.BMaxPower}.Write(&b)
	for _, iface := range d.Interfaces {
		for _, iad := range d.Associations {
			if iad.BFirstInterface == iface.Descriptor.BInterfaceNumber {
				iad.Write(&b)
			}
		}
		iface.Descriptor.Write(&b)
		if iface.HID != nil {
			hd, err := iface.HID.DescriptorBytes()
			if err != nil {
				t.Fatal(err)
			}
			b.Write(hd)
		}
		for _, cd := range iface.ClassDescriptors {
			b.Write(cd.Bytes())
		}
		for _, ep := range iface.Endpoints {
			ep.Write(&b)
		}
	}
	rebuilt := b.Bytes()
	if len(rebuilt) != len(original) {
		t.Fatalf("length %d, want %d", len(rebuilt), len(original))
	}
	for i := range original {
		if rebuilt[i] != original[i] && !(i >= 2 && original[i-2] == 0x01 && original[i-1] == 0x22) && !(i >= 3 && original[i-3] == 0x01 && original[i-2] == 0x22) {
			t.Fatalf("byte %d: %02X, want %02X", i, rebuilt[i], original[i])
		}
	}
	if d.Device.BcdUSB != 0x0200 || d.Device.IDProduct != 0x1304 || len(d.Interfaces) != 7 {
		t.Fatalf("%+v", d.Device)
	}
	vendor := parseReportSizes(reports[2])
	if !vendor.UsesIDs || vendor.Input[0x42] != 53 || vendor.Feature[1] != 63 || vendor.Output[0x87] != 63 || vendor.Input[0x40] == 0 {
		t.Fatalf("interface 2: %+v", vendor)
	}
}
