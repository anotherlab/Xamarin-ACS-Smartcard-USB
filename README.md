# Xamarin Wrapper for ACS Library

This is the Xamarin Android binding for [ACS's USB library for Android](https://www.acs.com.hk/en/mobile-support/).  This library allows Android developers to directly work with ACS Readers connected via USB.  Also included is a Xamarin C# port of the sample Java application that comes with the library.  This library provides low level access to ACS RFID card readers.  The developer will be responsible for communicating with the ACS reader and requesting data from it.

The .jar file that the library is compiled with is included in the repository.  It is part of an API kit that should be downloaded from [https://www.acs.com.hk/en/mobile-support/](https://www.acs.com.hk/en/mobile-support/).  The API kit includes the API documentation in HTML format.

## Structure

The solution contains two projects

* acs.library - A Binding library for the ACS jar file.  The version used was 1.1.3
* acs.Demo - A C# port of the original demo

## Getting started
## Getting Started
**1.** Reference the library to your project

**2.** Get a reference to the USB System Service
```C#
mManager = (UsbManager)GetSystemService(Context.UsbService);
```

**3.** Register a BroadcastReceiver to get USB Permission and connection messages
```C#
mPermissionIntent = PendingIntent.GetBroadcast(this, 0, new Intent(
        ACTION_USB_PERMISSION), 0);
IntentFilter filter = new IntentFilter();
filter.AddAction(ACTION_USB_PERMISSION);
filter.AddAction(UsbManager.ActionUsbAccessoryDetached);

mReceiver = new MyBroadcastReceiver();

RegisterReceiver(mReceiver, filter);
```

**4.** Refer to [MainActivity.cs](https://github.com/anotherlab/Xamarin-ACS-Smartcard-USB/blob/master/acs.Demo/MainActivity.cs) in the example app to see how to connect to a ACS RFID reader and read data from it.

## Compatible Devices

* [ACS ARC122U](https://www.acs.com.hk/en/products/3/acr122u-usb-nfc-reader/)

## Additional information

This library provides the low level access to an ACS RFID reader.  It does not provide any code for sending commands to the reader or parsing data from the reader.  The ACS readers follow the SmartCard protocols.  You would need to know to create and parse [APDU](https://en.wikipedia.org/wiki/Smart_card_application_protocol_data_unit) data packets.  It is also expected that you would use and follow the ACS API documentation

## Author, License, and Copyright

This library is licensed under LGPL Version 2.1. Please see LICENSE.txt for the complete license.

Copyright 2018, Tyler Technologies.  All Rights Reserved.  Portions of this library are based on ACS intellectual property.  Their rights remain intact.
