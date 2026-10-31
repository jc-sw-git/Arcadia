package main

import (
	"fmt"
	"regexp"
	"strconv"
	"strings"
	"sync"
	"unsafe"

	"golang.org/x/sys/windows"
)

// The real device's HID collections, opened through Windows' HID class driver. HidHide hides them from every
// process but this one (its allow list), so Steam only sees the emulated copy.

var (
	hidDLL                    = windows.NewLazySystemDLL("hid.dll")
	procHidDGetFeature        = hidDLL.NewProc("HidD_GetFeature")
	procHidDSetFeature        = hidDLL.NewProc("HidD_SetFeature")
	procHidDGetInputReport    = hidDLL.NewProc("HidD_GetInputReport")
	procHidDSetOutputReport   = hidDLL.NewProc("HidD_SetOutputReport")
	procHidDGetSerialNumber   = hidDLL.NewProc("HidD_GetSerialNumberString")
	procHidDGetPreparsedData  = hidDLL.NewProc("HidD_GetPreparsedData")
	procHidDFreePreparsedData = hidDLL.NewProc("HidD_FreePreparsedData")
	procHidPGetCaps           = hidDLL.NewProc("HidP_GetCaps")
	hidInterfaceGUID          = windows.GUID{Data1: 0x4D1E55B2, Data2: 0xF16F, Data3: 0x11CF, Data4: [8]byte{0x88, 0xCB, 0x00, 0x11, 0x11, 0x00, 0x00, 0x30}}
	interfaceNumberInPath     = regexp.MustCompile(`(?i)&mi_([0-9a-f]{2})`)
)

type hidCaps struct {
	Usage, UsagePage                         uint16
	InputLength, OutputLength, FeatureLength uint16
	_                                        [17]uint16
	_                                        [10]uint16
}

type collection struct {
	Path      string
	Interface uint8
	Handle    windows.Handle
	Caps      hidCaps
	writeLock sync.Mutex
}

// findCollections lists the HID collections of the real device, by interface number. Must run before the
// emulated copy is attached, since that has the same vendor and product id.
func findCollections(vidPid string) (map[uint8][]*collection, error) {
	paths, err := windows.CM_Get_Device_Interface_List("", &hidInterfaceGUID, windows.CM_GET_DEVICE_INTERFACE_LIST_PRESENT)
	if err != nil {
		return nil, fmt.Errorf("list HID devices: %w", err)
	}
	found := map[uint8][]*collection{}
	for _, path := range paths {
		if !strings.Contains(strings.ToLower(path), vidPid) {
			continue
		}
		// A device with one interface (the Triton by cable, #249) has no &mi_ in its paths: it is interface 0.
		number := uint64(0)
		if match := interfaceNumberInPath.FindStringSubmatch(path); match != nil {
			number, _ = strconv.ParseUint(match[1], 16, 8)
		}
		found[uint8(number)] = append(found[uint8(number)], &collection{Path: path, Interface: uint8(number)})
	}
	return found, nil
}

// open opens the collection for reading and writing; Windows refuses that for keyboards and mice (the lizard
// mode interfaces), which are then opened without access, enough for feature reports only.
func (c *collection) open() error {
	path, err := windows.UTF16PtrFromString(c.Path)
	if err != nil {
		return err
	}
	share := uint32(windows.FILE_SHARE_READ | windows.FILE_SHARE_WRITE)
	c.Handle, err = windows.CreateFile(path, windows.GENERIC_READ|windows.GENERIC_WRITE, share, nil, windows.OPEN_EXISTING, windows.FILE_FLAG_OVERLAPPED, 0)
	if err != nil {
		var second error
		c.Handle, second = windows.CreateFile(path, 0, share, nil, windows.OPEN_EXISTING, windows.FILE_FLAG_OVERLAPPED, 0)
		if second != nil {
			return fmt.Errorf("open: %w", err)
		}
	}
	var preparsed uintptr
	if ok, _, _ := procHidDGetPreparsedData.Call(uintptr(c.Handle), uintptr(unsafe.Pointer(&preparsed))); ok == 0 {
		return fmt.Errorf("HidD_GetPreparsedData failed")
	}
	defer procHidDFreePreparsedData.Call(preparsed)
	procHidPGetCaps.Call(preparsed, uintptr(unsafe.Pointer(&c.Caps)))
	return nil
}

func (c *collection) close() {
	if c.Handle != 0 {
		windows.CloseHandle(c.Handle)
	}
}

// read blocks until the next input report; the first byte is the report id (0 without ids).
func (c *collection) read(buffer []byte) (int, error) {
	event, err := windows.CreateEvent(nil, 1, 0, nil)
	if err != nil {
		return 0, err
	}
	defer windows.CloseHandle(event)
	overlapped := windows.Overlapped{HEvent: event}
	var n uint32
	err = windows.ReadFile(c.Handle, buffer, &n, &overlapped)
	if err == windows.ERROR_IO_PENDING {
		err = windows.GetOverlappedResult(c.Handle, &overlapped, &n, true)
	}
	return int(n), err
}

func (c *collection) write(buffer []byte) error {
	c.writeLock.Lock()
	defer c.writeLock.Unlock()
	event, err := windows.CreateEvent(nil, 1, 0, nil)
	if err != nil {
		return err
	}
	defer windows.CloseHandle(event)
	overlapped := windows.Overlapped{HEvent: event}
	var n uint32
	err = windows.WriteFile(c.Handle, buffer, &n, &overlapped)
	if err == windows.ERROR_IO_PENDING {
		err = windows.GetOverlappedResult(c.Handle, &overlapped, &n, true)
	}
	return err
}

func hidCall(proc *windows.LazyProc, handle windows.Handle, buffer []byte) error {
	if len(buffer) == 0 {
		return fmt.Errorf("empty buffer")
	}
	if ok, _, err := proc.Call(uintptr(handle), uintptr(unsafe.Pointer(&buffer[0])), uintptr(len(buffer))); ok == 0 {
		return err
	}
	return nil
}

func (c *collection) getFeature(buffer []byte) error {
	return hidCall(procHidDGetFeature, c.Handle, buffer)
}
func (c *collection) setFeature(buffer []byte) error {
	return hidCall(procHidDSetFeature, c.Handle, buffer)
}
func (c *collection) getInput(buffer []byte) error {
	return hidCall(procHidDGetInputReport, c.Handle, buffer)
}
func (c *collection) setOutput(buffer []byte) error {
	return hidCall(procHidDSetOutputReport, c.Handle, buffer)
}

// serialNumber is the USB serial number string of the device the collection belongs to. Never logged.
func (c *collection) serialNumber() string {
	buffer := make([]uint16, 128)
	if ok, _, _ := procHidDGetSerialNumber.Call(uintptr(c.Handle), uintptr(unsafe.Pointer(&buffer[0])), uintptr(len(buffer)*2)); ok == 0 {
		return ""
	}
	return windows.UTF16ToString(buffer)
}
