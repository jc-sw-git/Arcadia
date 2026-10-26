//go:build !windows

package main

import "errors"

// Only so the pure parts (descriptors, report sizes, buffers) can be tested on Linux; the proxy runs on Windows only.

var qpcFrequency int64 = 1

func qpcNow() int64 { return 0 }

type hidCaps struct {
	Usage, UsagePage                         uint16
	InputLength, OutputLength, FeatureLength uint16
}

type collection struct {
	Path      string
	Interface uint8
	Caps      hidCaps
}

var errWindowsOnly = errors.New("windows only")

func (c *collection) read([]byte) (int, error) { return 0, errWindowsOnly }
func (c *collection) write([]byte) error       { return errWindowsOnly }
func (c *collection) getFeature([]byte) error  { return errWindowsOnly }
func (c *collection) setFeature([]byte) error  { return errWindowsOnly }
func (c *collection) getInput([]byte) error    { return errWindowsOnly }
func (c *collection) setOutput([]byte) error   { return errWindowsOnly }

func main() {}
