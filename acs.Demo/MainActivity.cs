using System;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Gestures;
using Android.Hardware.Usb;
using Android.Widget;
using Android.OS;
using Android.Views;
using Com.Acs.Smartcard;
using Java.IO;
using Console = System.Console;
using Reader = Com.Acs.Smartcard.Reader;
using Java.Lang;
using Exception = System.Exception;
using Object = Java.Lang.Object;

namespace acs.Demo
{
    [Activity(Label = "acs.Demo", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity
    {
        private static string ACTION_USB_PERMISSION = "com.android.example.USB_PERMISSION";

        private static string[] powerActionStrings = { "Power Down",
            "Cold Reset", "Warm Reset" };

        private static string[] stateStrings = { "Unknown", "Absent",
            "Present", "Swallowed", "Powered", "Negotiable", "Specific" };

        private static string[] featureStrings = { "FEATURE_UNKNOWN",
            "FEATURE_VERIFY_PIN_START", "FEATURE_VERIFY_PIN_FINISH",
            "FEATURE_MODIFY_PIN_START", "FEATURE_MODIFY_PIN_FINISH",
            "FEATURE_GET_KEY_PRESSED", "FEATURE_VERIFY_PIN_DIRECT",
            "FEATURE_MODIFY_PIN_DIRECT", "FEATURE_MCT_READER_DIRECT",
            "FEATURE_MCT_UNIVERSAL", "FEATURE_IFD_PIN_PROPERTIES",
            "FEATURE_ABORT", "FEATURE_SET_SPE_MESSAGE",
            "FEATURE_VERIFY_PIN_DIRECT_APP_ID",
            "FEATURE_MODIFY_PIN_DIRECT_APP_ID", "FEATURE_WRITE_DISPLAY",
            "FEATURE_GET_KEY", "FEATURE_IFD_DISPLAY_PROPERTIES",
            "FEATURE_GET_TLV_PROPERTIES", "FEATURE_CCID_ESC_COMMAND" };

        private static string[] propertyStrings = { "Unknown", "wLcdLayout",
            "bEntryValidationCondition", "bTimeOut2", "wLcdMaxCharacters",
            "wLcdMaxLines", "bMinPINSize", "bMaxPINSize", "sFirmwareID",
            "bPPDUSupport", "dwMaxAPDUDataSize", "wIdVendor", "wIdProduct" };

        private const int DIALOG_VERIFY_PIN_ID = 0;
        private const int DIALOG_MODIFY_PIN_ID = 1;
        private const int DIALOG_READ_KEY_ID = 2;
        private const int DIALOG_DISPLAY_LCD_MESSAGE_ID = 3;

        private UsbManager mManager;
        public Reader mReader;
        private PendingIntent mPermissionIntent;

        private static int MAX_LINES = 25;
        private TextView mResponseTextView;
        private Spinner mReaderSpinner;
        private ArrayAdapter<string> mReaderAdapter;
        private Spinner mSlotSpinner;
        private ArrayAdapter<string> mSlotAdapter;
        private Spinner mPowerSpinner;
        private Button mListButton;
        private Button mOpenButton;
        private Button mCloseButton;
        private Button mGetStateButton;
        private Button mPowerButton;
        private Button mGetAtrButton;
        private CheckBox mT0CheckBox;
        private CheckBox mT1CheckBox;
        private Button mSetProtocolButton;
        private Button mGetProtocolButton;
        private EditText mCommandEditText;
        private Button mTransmitButton;
        private EditText mControlEditText;
        private Button mControlButton;
        private Button mGetFeaturesButton;
        private Button mVerifyPinButton;
        private Button mModifyPinButton;
        private Button mReadKeyButton;
        private Button mDisplayLcdMessageButton;

        private Button GetIdButton;

        private Features mFeatures = new Features();
        private PinVerify mPinVerify = new PinVerify();
        private PinModify mPinModify = new PinModify();
        private ReadKeyOption mReadKeyOption = new ReadKeyOption();
        private string mLcdMessage;

        private readonly object syncLock = new object();

        private MyBroadcastReceiver mReceiver;

        public class MyBroadcastReceiver : BroadcastReceiver
        {
            public override void OnReceive(Context context, Intent intent)
            {
                string action = intent.Action;
                MainActivity a = (MainActivity) context;

                if (ACTION_USB_PERMISSION.Equals(action))
                {
                    lock (a.syncLock)
                    {
                        UsbDevice device = (UsbDevice) intent.GetParcelableExtra(UsbManager.ExtraDevice);

                        if (intent.GetBooleanExtra(UsbManager.ExtraPermissionGranted, false))
                        {
                            if (device != null)
                            {
                                // make async
                                a.logMsg("Opening reader: " + device.DeviceName + "...");

                                new OpenTask(a).Execute(device);
                            }
                        }
                        else
                        {
                            a.logMsg("Permission denied for device "
                                     + device.DeviceName);

                            // Enable open button
                            a.mOpenButton.Enabled = true;
                        }
                    }
                }
                else if (UsbManager.ActionUsbAccessoryDetached.Equals(action))
                {
                    lock (a.syncLock)
                    {
                        // Update reader list
                        a.mReaderAdapter.Clear();

                        foreach (var dev in a.mManager.DeviceList.Values)
                        {
                            if (a.mReader.IsSupported(dev))
                            {
                                a.mReaderAdapter.Add(dev.DeviceName);
                            }
                        }

                        UsbDevice device = (UsbDevice)intent.GetParcelableExtra(UsbManager.ExtraDevice);

                        if (device != null && device.Equals(a.mReader.Device))
                        {

                            // Disable buttons
                            a.SetButtons(false);

                            // Clear slot items
                            a.mSlotAdapter.Clear();

                            // Close reader
                            a.logMsg("Closing reader...");

                            //a.CloseTask(a);
                            new CloseTask(a).Execute();
                        }
                    }
                }
            }
        }

        private class OpenTask : AsyncTask<UsbDevice, Java.Lang.Object, Java.Lang.Exception>
        {
            private MainActivity a;

            public OpenTask(Context context)
            {
                a = (MainActivity)context;
            }
            protected override Java.Lang.Exception RunInBackground(params UsbDevice[] @params)
            {
                Java.Lang.Exception result = null;

                try
                {
                    a.mReader.Open(@params[0]);
                }
                catch (Java.Lang.Exception e)
                {

                    result = e;
                }

                return result;
            }

            protected override void OnPostExecute(Java.Lang.Exception result)
            {
                if (result != null)
                {

                    a.logMsg(result.ToString());

                }
                else
                {

                    a.logMsg("Reader name: " + a.mReader.ReaderName);

                    int numSlots = a.mReader.NumSlots;
                    a.logMsg("Number of slots: " + numSlots);

                    // Add slot items
                    a.mSlotAdapter.Clear();
                    for (int i = 0; i < numSlots; i++)
                    {
                        a.mSlotAdapter.Add(Integer.ToString(i));
                    }

                    // Remove all control codes
                    a.mFeatures.Clear();

                    // Enable buttons
                    a.SetButtons(true);
                }
            }
        }

        private class CloseTask : AsyncTask<int, int, int> 
        {
            private MainActivity a;

            public CloseTask(Context context)
            {
                a = (MainActivity)context;
            }
            protected override int RunInBackground(params int[] @params)
            {

                a.mReader.Close();
                return 0;
            }

            protected override void OnPostExecute(Java.Lang.Object result)
            {
                a.mOpenButton.Enabled = (true);
            }

        }

        public void SetButtons(bool Enabled)
        {
            mCloseButton.Enabled = Enabled;
            mSlotSpinner.Enabled = Enabled;
            mGetStateButton.Enabled = Enabled;
            mPowerSpinner.Enabled = Enabled;
            mPowerButton.Enabled = Enabled;
            mGetAtrButton.Enabled = Enabled;
            mT0CheckBox.Enabled = Enabled;
            mT1CheckBox.Enabled = Enabled;
            mSetProtocolButton.Enabled = Enabled;
            mGetProtocolButton.Enabled = Enabled;
            mTransmitButton.Enabled = Enabled;
            mControlButton.Enabled = Enabled;
            mGetFeaturesButton.Enabled = Enabled;
            mVerifyPinButton.Enabled = Enabled;
            mModifyPinButton.Enabled = Enabled;
            mReadKeyButton.Enabled = Enabled;
            mDisplayLcdMessageButton.Enabled = Enabled;
        }
        private class PowerParams
        {

            public int slotNum;
            public int action;
        }

        private class PowerResult
        {

            public byte[] atr;
            public Java.Lang.Exception e;
        }

        private class PowerTask : AsyncTask<PowerParams, Int16, PowerResult>
        {
            private MainActivity a;

            public PowerTask(Context context)
            {
                a = (MainActivity)context;
            }

            protected override PowerResult RunInBackground(params PowerParams[] @params)
            {
                PowerResult result = new PowerResult();

                try
                {

                    result.atr = a.mReader.Power(@params[0].slotNum, @params[0].action);

                } catch (Java.Lang.Exception e) {

                    result.e = e;
                }

                return result;
            }

            protected override void OnPostExecute(PowerResult result)
            {
                if (result.e != null)
                {
                    a.logMsg(result.e.ToString());
                }
                else
                {
                    // Show ATR
                    if (result.atr != null)
                    {
                        a.logMsg("ATR:");
                        a.logBuffer(result.atr, result.atr.Length);
                    }
                    else
                    {
                        a.logMsg("ATR: None");
                    }
                }
            }
        }

        private class SetProtocolParams
        {
            public int slotNum;
            public int preferredProtocols;
        }

        private class SetProtocolResult
        {
            public int activeProtocol;
            public Java.Lang.Exception e;
        }

        private class SetProtocolTask : AsyncTask<SetProtocolParams, Java.Lang.Object, SetProtocolResult>
        {
            private MainActivity a;

            public SetProtocolTask(Context context)
            {
                a = (MainActivity)context;
            }
            protected override SetProtocolResult RunInBackground(params SetProtocolParams[] @params)
            {
                SetProtocolResult result = new SetProtocolResult();

                try
                {

                    result.activeProtocol = a.mReader.SetProtocol(@params[0].slotNum,
                        @params[0].preferredProtocols);

                } catch (Java.Lang.Exception e) {

                    result.e = e;
                }

                return result;
            }
            protected override void OnPostExecute(SetProtocolResult result)
            {
                if (result.e != null)
                {

                    a.logMsg(result.e.ToString());

                }
                else
                {

                    string activeProtocolString = "Active Protocol: ";

                    switch (result.activeProtocol)
                    {

                        case Reader.ProtocolT0:
                            activeProtocolString += "T=0";
                            break;

                        case Reader.ProtocolT1:
                            activeProtocolString += "T=1";
                            break;

                        default:
                            activeProtocolString += "Unknown";
                            break;
                    }

                    // Show active protocol
                    a.logMsg(activeProtocolString);
                }
            }

        }

        private class TransmitParams
        {
            public int slotNum;
            public int controlCode;
            public string commandString;
        }

        private class TransmitProgress
        {

            public int controlCode;
            public byte[] command;
            public int commandLength;
            public byte[] response;
            public int responseLength;
            public Java.Lang.Exception e;
        }

        private class TransmitTask : AsyncTask<TransmitParams, TransmitProgress, int>
        {
            private MainActivity a;

            public TransmitTask(Context context)
            {
                a = (MainActivity) context;
            }

            protected override int RunInBackground(params TransmitParams[] @params)
            {
                TransmitProgress progress = null;

                byte[] command = null;
                byte[] response = null;
                int responseLength = 0;
                int foundIndex = 0;
                int startIndex = 0;

                do
                {

                    // Find carriage return
                    foundIndex = @params[0].commandString.IndexOf('\n', startIndex);
                    if (foundIndex >= 0)
                    {
                        command = a.toByteArray(@params[0].commandString.Substring(
                            startIndex, foundIndex));
                    }
                    else
                    {
                        command = a.toByteArray(@params[0].commandString
                            .Substring(startIndex));
                    }

                    // Set next start index
                    startIndex = foundIndex + 1;

                    response = new byte[300];
                    progress = new TransmitProgress {controlCode = @params[0].controlCode};

                    try
                    {

                        if (@params[0].controlCode < 0)
                        {

                            // Transmit APDU
                            responseLength = a.mReader.Transmit(@params[0].slotNum,
                                command, command.Length, response,
                                response.Length);

                        }
                        else
                        {

                            // Transmit control command
                            responseLength = a.mReader.Control(@params[0].slotNum,
                                @params[0].controlCode, command, command.Length,
                                response, response.Length);
                        }

                        progress.command = command;
                        progress.commandLength = command.Length;
                        progress.response = response;
                        progress.responseLength = responseLength;
                        progress.e = null;

                    }
                    catch (Java.Lang.Exception e)
                    {

                        progress.command = null;
                        progress.commandLength = 0;
                        progress.response = null;
                        progress.responseLength = 0;
                        progress.e = e;
                    }

                    PublishProgress(progress);

                    // Needs to be ported
                    //publishProgress(progress);

                } while (foundIndex >= 0);

                return 0;

            }

            protected override void OnProgressUpdate(params TransmitProgress[] progress)
            {
                if (progress[0].e != null)
                {
                    a.logMsg(progress[0].e.ToString());
                }
                else
                {
                    a.logMsg("Command:");
                    a.logBuffer(progress[0].command, progress[0].commandLength);

                    a.logMsg("Response:");
                    a.logBuffer(progress[0].response, progress[0].responseLength);

                    if (progress[0].response != null
                        && progress[0].responseLength > 0)
                    {

                        int controlCode;
                        int i;

                        // Show control codes for IOCTL_GET_FEATURE_REQUEST
                        if (progress[0].controlCode == Reader.IoctlGetFeatureRequest)
                        {

                            a.mFeatures.FromByteArray(progress[0].response,
                                progress[0].responseLength);

                            a.logMsg("Features:");
                            for (i = Features.FeatureVerifyPinStart; i <= Features.FeatureCcidEscCommand; i++)
                            {

                                controlCode = a.mFeatures.GetControlCode(i);
                                if (controlCode >= 0)
                                {
                                    a.logMsg("Control Code: " + controlCode + " ("
                                           + featureStrings[i] + ")");
                                }
                            }

                            // Enable buttons if features are supported
                            a.mVerifyPinButton.Enabled = a.mFeatures.GetControlCode(Features.FeatureVerifyPinDirect) >= 0;
                            a.mModifyPinButton.Enabled = a.mFeatures.GetControlCode(Features.FeatureModifyPinDirect) >= 0;
                        }

                        controlCode = a.mFeatures.GetControlCode(Features.FeatureIfdPinProperties);

                        if (controlCode >= 0
                            && progress[0].controlCode == controlCode)
                        {

                            PinProperties pinProperties = new PinProperties(
                                progress[0].response,
                                progress[0].responseLength);

                            a.logMsg("PIN Properties:");
                            a.logMsg($"LCD Layout: {pinProperties.LcdLayout:x2}");
                            a.logMsg($"Entry Validation Condition: {pinProperties.EntryValidationCondition:x2}");
                            a.logMsg($"Timeout 2: {pinProperties.TimeOut2:x2}");
                        }

                        controlCode = a.mFeatures.GetControlCode(Features.FeatureGetTlvProperties);

                        if (controlCode >= 0
                                && progress[0].controlCode == controlCode)
                        {

                            TlvProperties readerProperties = new TlvProperties(
                                    progress[0].response,
                                    progress[0].responseLength);

                            //Java.Lang.Object property;
                            a.logMsg("TLV Properties:");
                            for (i = TlvProperties.PROPERTYWLcdLayout; i <= TlvProperties.PROPERTYWIdProduct; i++)
                            {

                                var property = readerProperties.GetProperty(i);
                                if (property is Java.Lang.Integer)
                                {
                                    a.logMsg(propertyStrings[i] + $": {property:x2}");

                                }
                                else if (property is Java.Lang.String)
                                {
                                    a.logMsg(propertyStrings[i] + ": " + property);
                                }
                            }
                        }
                    }
                }
            }
        }

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            this.SetContentView(Resource.Layout.Main);

            // Set our view from the "main" layout resource
            // SetContentView (Resource.Layout.YourMainView);

            mManager = (UsbManager)GetSystemService(Context.UsbService);

            mReader = new Reader(mManager);

            mReader.StateChange += MReader_StateChange;


            // Register receiver for USB permission
            mPermissionIntent = PendingIntent.GetBroadcast(this, 0, new Intent(
                    ACTION_USB_PERMISSION), 0);
            IntentFilter filter = new IntentFilter();
            filter.AddAction(ACTION_USB_PERMISSION);
            filter.AddAction(UsbManager.ActionUsbAccessoryDetached);

            mReceiver = new MyBroadcastReceiver();

            RegisterReceiver(mReceiver, filter);

            // Initialize response text view
            mResponseTextView = (TextView)FindViewById(Resource.Id.main_text_view_response);
            mResponseTextView.MovementMethod = new Android.Text.Method.ScrollingMovementMethod();
            mResponseTextView.SetMaxLines(MAX_LINES);
            mResponseTextView.Text= ("");

            // Initialize reader spinner
            mReaderAdapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem);
            foreach (UsbDevice device in mManager.DeviceList.Values)
            {
                if (mReader.IsSupported(device))
                {
                    mReaderAdapter.Add(device.DeviceName);
                }
            }
            mReaderSpinner = (Spinner)FindViewById(Resource.Id.main_spinner_reader);
            mReaderSpinner.Adapter = (mReaderAdapter);

            // Initialize slot spinner
            mSlotAdapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem);
            mSlotSpinner = (Spinner)FindViewById(Resource.Id.main_spinner_slot);
            mSlotSpinner.Adapter =(mSlotAdapter);

            // Initialize power spinner
            ArrayAdapter<string> powerAdapter = new ArrayAdapter<string>(this,
                    Android.Resource.Layout.SimpleSpinnerItem, powerActionStrings);
            mPowerSpinner = (Spinner)FindViewById(Resource.Id.main_spinner_power);
            mPowerSpinner.Adapter= (powerAdapter);
            mPowerSpinner.SetSelection(Reader.CardWarmReset);

            // Initialize list button
            mListButton = (Button)FindViewById(Resource.Id.main_button_list);
            mListButton.Click += delegate(object sender, EventArgs args)
            {
                mReaderAdapter.Clear();
                foreach (UsbDevice device in mManager.DeviceList.Values)
                {
                    if (mReader.IsSupported(device))
                    {
                        mReaderAdapter.Add(device.DeviceName);
                    }
                }
            };

            // Initialize open button
            mOpenButton = (Button)FindViewById(Resource.Id.main_button_open);
            mOpenButton.Click += delegate(object sender, EventArgs args)
            {
                bool requested = false;

                // Disable open button
                mOpenButton.Enabled = (false);

                string deviceName = (string) mReaderSpinner.SelectedItem;

                if (deviceName != null)
                {

                    // For each device
                    foreach (UsbDevice device in mManager.DeviceList.Values)
                    {

                        // If device name is found
                        if (deviceName.Equals(device.DeviceName))
                        {

                            // Request permission
                            mManager.RequestPermission(device,
                                    mPermissionIntent);

                            requested = true;
                            break;
                        }
                    }
                }

                if (!requested)
                {

                    // Enable open button
                    mOpenButton.Enabled = (true);
                }

            };

            // Initialize close button
            mCloseButton = (Button)FindViewById(Resource.Id.main_button_close);
            mCloseButton.Click += delegate(object sender, EventArgs args)
            {
                // Disable buttons
                SetButtons(false);

                // Clear slot items
                mSlotAdapter.Clear();

                // Close reader
                logMsg("Closing reader...");
                new CloseTask(this).Execute();
            };

            // Initialize get state button
            mGetStateButton = (Button)FindViewById(Resource.Id.main_button_get_state);
            mGetStateButton.Click += delegate(object sender, EventArgs args)
            {
                // Get slot number
                int slotNum = mSlotSpinner.SelectedItemPosition;

                // If slot is selected
                if (slotNum != Spinner.InvalidPosition)
                {
                    try
                    {

                        // Get state
                        logMsg("Slot " + slotNum + ": Getting state...");
                        int state = mReader.GetState(slotNum);

                        if (state < Reader.CardUnknown
                                || state > Reader.CardSpecific)
                        {
                            state = Reader.CardUnknown;
                        }

                        logMsg("State: " + stateStrings[state]);

                    }
                    catch (IllegalArgumentException e)
                    {
                        logMsg(e.ToString());
                    }
                }
            };

            // Initialize power button
            mPowerButton = (Button)FindViewById(Resource.Id.main_button_power);
            mPowerButton.Click += delegate(object sender, EventArgs args)
            {
                // Get slot number
                int slotNum = mSlotSpinner.SelectedItemPosition;

                // Get action number
                int actionNum = mPowerSpinner.SelectedItemPosition;

                // If slot and action are selected
                if (slotNum != Spinner.InvalidPosition
                        && actionNum != Spinner.InvalidPosition)
                {

                    if (actionNum < Reader.CardPowerDown
                            || actionNum > Reader.CardWarmReset)
                    {
                        actionNum = Reader.CardWarmReset;
                    }

                    // Set parameters
                    PowerParams _params = new PowerParams();
                    _params.slotNum = slotNum;
                    _params.action = actionNum;

                    // Perform power action
                    logMsg("Slot " + slotNum + ": "
                            + powerActionStrings[actionNum] + "...");
                    new PowerTask(this).Execute(_params);
                }
            };

            mGetAtrButton = (Button)FindViewById(Resource.Id.main_button_get_atr);
            mGetAtrButton.Click += delegate(object sender, EventArgs args)
            {
                // Get slot number
                int slotNum = mSlotSpinner.SelectedItemPosition;

                // If slot is selected
                if (slotNum != Spinner.InvalidPosition)
                {
                    try
                    {
                        // Get ATR
                        logMsg("Slot " + slotNum + ": Getting ATR...");
                        byte[] atr = mReader.GetAtr(slotNum);

                        // Show ATR
                        if (atr != null)
                        {
                            logMsg("ATR:");
                            logBuffer(atr, atr.Length);
                        }
                        else
                        {
                            logMsg("ATR: None");
                        }
                    }
                    catch (IllegalArgumentException e)
                    {
                        logMsg(e.ToString());
                    }
                }
            };
            // Initialize T=0 check box
            mT0CheckBox = (CheckBox)FindViewById(Resource.Id.main_check_box_t0);
            mT0CheckBox.Checked = (true);

            // Initialize T=1 check box
            mT1CheckBox = (CheckBox)FindViewById(Resource.Id.main_check_box_t1);
            mT1CheckBox.Checked= (true);

            // Initialize set protocol button
            mSetProtocolButton = (Button)FindViewById(Resource.Id.main_button_set_protocol);
            mSetProtocolButton.Click += delegate(object sender, EventArgs args)
            {
                // Get slot number
                int slotNum = mSlotSpinner.SelectedItemPosition;

                // If slot is selected
                if (slotNum != Spinner.InvalidPosition)
                {

                    int preferredProtocols = Reader.ProtocolUndefined;
                    string preferredProtocolsString = "";

                    if (mT0CheckBox.Checked)
                    {

                        preferredProtocols |= Reader.ProtocolT0;
                        preferredProtocolsString = "T=0";
                    }

                    if (mT1CheckBox.Checked)
                    {

                        preferredProtocols |= Reader.ProtocolT1;
                        if (preferredProtocolsString != "")
                        {
                            preferredProtocolsString += "/";
                        }

                        preferredProtocolsString += "T=1";
                    }

                    if (preferredProtocolsString == "")
                    {
                        preferredProtocolsString = "None";
                    }

                    // Set Parameters
                    SetProtocolParams _params = new SetProtocolParams();
                    _params.slotNum = slotNum;
                    _params.preferredProtocols = preferredProtocols;

                    // Set protocol
                    logMsg("Slot " + slotNum + ": Setting protocol to "
                            + preferredProtocolsString + "...");
                    new SetProtocolTask(this).Execute(_params);
                }
            };

            // Initialize get active protocol button
            mGetProtocolButton = (Button)FindViewById(Resource.Id.main_button_get_protocol);
            mGetProtocolButton.Click += delegate(object sender, EventArgs args)
            {
                // Get slot number
                int slotNum = mSlotSpinner.SelectedItemPosition;

                // If slot is selected
                if (slotNum != Spinner.InvalidPosition)
                {
                    try
                    {
                        // Get active protocol
                        logMsg("Slot " + slotNum
                                + ": Getting active protocol...");
                        int activeProtocol = mReader.GetProtocol(slotNum);

                        // Show active protocol
                        string activeProtocolString = "Active Protocol: ";
                        switch (activeProtocol)
                        {

                            case Reader.ProtocolT0:
                                activeProtocolString += "T=0";
                                break;

                            case Reader.ProtocolT1:
                                activeProtocolString += "T=1";
                                break;

                            default:
                                activeProtocolString += "Unknown";
                                break;
                        }

                        logMsg(activeProtocolString);

                    }
                    catch (IllegalArgumentException e)
                    {
                        logMsg(e.ToString());
                    }
                }
            };

            // Initialize command edit text
            mCommandEditText = (EditText)FindViewById(Resource.Id.main_edit_text_command);

            GetIdButton = FindViewById<Button>(Resource.Id.main_button_get_id);

            GetIdButton.Click += delegate(object sender, EventArgs args)
            {
                // The APDU byte array to get the UID from the card
                var command = new byte[] { 0xFF, 0xCA, 0x00, 0x00, 0x00 };

                // The byte array to contain the response
                var response = new byte[300];

                int slotNum = mSlotSpinner.SelectedItemPosition;

                // If slot is selected
                if (slotNum != Spinner.InvalidPosition)
                {
                    try
                    {
                        var atr = mReader.Power(slotNum, Reader.CardWarmReset);

                        mReader.SetProtocol(slotNum, Reader.ProtocolT0 | Reader.ProtocolT1);

                        // Send the command to the reader
                        var responseLength = mReader.Transmit(slotNum,
                            command, command.Length, response,
                            response.Length);

                        if (responseLength > 0)
                        {
                            var s = toHexString(response);
                            logMsg(s);
                        }
                    }
                    catch (Java.Lang.Exception e)
                    {
                        logMsg(e.Message);
                    }

                }



            };



            // Initialize transmit button
            mTransmitButton = (Button)FindViewById(Resource.Id.main_button_transmit);
            mTransmitButton.Click += delegate(object sender, EventArgs args)
            {
                // Get slot number
                int slotNum = mSlotSpinner.SelectedItemPosition;

                // If slot is selected
                if (slotNum != Spinner.InvalidPosition)
                {

                    // Set parameters
                    TransmitParams _params = new TransmitParams();
                    _params.slotNum = slotNum;
                    _params.controlCode = -1;
                    _params.commandString = mCommandEditText.Text;

                    // Transmit APDU
                    logMsg("Slot " + slotNum + ": Transmitting APDU...");
                    new TransmitTask(this).Execute(_params);
                }
            };

            // Initialize control edit text
            mControlEditText = (EditText)FindViewById(Resource.Id.main_edit_text_control);
            mControlEditText.Text = (Integer.ToString(Reader.IoctlCcidEscape));

            // Initialize control button
            mControlButton = (Button)FindViewById(Resource.Id.main_button_control);
            mControlButton.Click += delegate(object sender, EventArgs args)
            {
                // Get slot number
                int slotNum = mSlotSpinner.SelectedItemPosition;

                // If slot is selected
                if (slotNum != Spinner.InvalidPosition)
                {
                    // Get control code
                    int controlCode;
                    try
                    {
                        controlCode = Integer.ParseInt(mControlEditText.Text);
                    }
                    catch (NumberFormatException e)
                    {
                        controlCode = Reader.IoctlCcidEscape;
                    }

                    // Set parameters
                    TransmitParams _params = new TransmitParams();
                    _params.slotNum = slotNum;
                    _params.controlCode = controlCode;
                    _params.commandString = mCommandEditText.Text;

                    // Transmit control command
                    logMsg("Slot " + slotNum
                            + ": Transmitting control command (Control Code: "
                            + _params.controlCode + ")...");
                    new TransmitTask(this).Execute(_params);
                }
            };

            // Initialize get features button
            mGetFeaturesButton = (Button)FindViewById(Resource.Id.main_button_get_features);
            mGetFeaturesButton.Click += delegate(object sender, EventArgs args)
            {
                // Get slot number
                int slotNum = mSlotSpinner.SelectedItemPosition;

                // If slot is selected
                if (slotNum != Spinner.InvalidPosition)
                {

                    // Set parameters
                    TransmitParams _params = new TransmitParams();
                    _params.slotNum = slotNum;
                    _params.controlCode = Reader.IoctlGetFeatureRequest;
                    _params.commandString = "";

                    // Transmit control command
                    logMsg("Slot " + slotNum
                            + ": Getting features (Control Code: "
                            + _params.controlCode + ")...");
                    new TransmitTask(this).Execute(_params);
                }
            };

            // PIN verification command (ACOS3)
            byte[] pinVerifyData = { (byte) 0x80, 0x20, 0x06, 0x00, 0x08,
                (byte) 0xFF, (byte) 0xFF, (byte) 0xFF, (byte) 0xFF,
                (byte) 0xFF, (byte) 0xFF, (byte) 0xFF, (byte) 0xFF };

            // Initialize PIN verify structure (ACOS3)
            mPinVerify.TimeOut=(0);
            mPinVerify.TimeOut2=(0);
            mPinVerify.FormatString=(0);
            mPinVerify.PinBlockString=(0x08);
            mPinVerify.PinLengthFormat=(0);
            mPinVerify.PinMaxExtraDigit=(0x0408);
            mPinVerify.EntryValidationCondition=(0x03);
            mPinVerify.NumberMessage=(0x01);
            mPinVerify.LangId=(0x0409);
            mPinVerify.MsgIndex=(0);
            mPinVerify.SetTeoPrologue(0, 0);
            mPinVerify.SetTeoPrologue(1, 0);
            mPinVerify.SetTeoPrologue(2, 0);
            mPinVerify.SetData(pinVerifyData, pinVerifyData.Length);

            // Initialize verify pin button
            mVerifyPinButton = (Button)FindViewById(Resource.Id.main_button_verify_pin);
            mVerifyPinButton.Click += delegate(object sender, EventArgs args)
            {
                ShowDialog(DIALOG_VERIFY_PIN_ID);
            };

            // PIN modification command (ACOS3)
            byte[] pinModifyData = { (byte) 0x80, 0x24, 0x00, 0x00, 0x08,
                (byte) 0xFF, (byte) 0xFF, (byte) 0xFF, (byte) 0xFF,
                (byte) 0xFF, (byte) 0xFF, (byte) 0xFF, (byte) 0xFF };

            // Initialize PIN modify structure (ACOS3)
            mPinModify.TimeOut=(0);
            mPinModify.TimeOut2 = (0);
            mPinModify.FormatString = (0);
            mPinModify.PinBlockString = (0x08);
            mPinModify.PinLengthFormat = (0);
            mPinModify.InsertionOffsetOld = (0);
            mPinModify.InsertionOffsetNew = (0);
            mPinModify.PinMaxExtraDigit = (0x0408);
            mPinModify.ConfirmPin = (0x01);
            mPinModify.EntryValidationCondition = (0x03);
            mPinModify.NumberMessage = (0x02);
            mPinModify.LangId = (0x0409);
            mPinModify.MsgIndex1 = (0);
            mPinModify.MsgIndex2 = (0x01);
            mPinModify.MsgIndex3 = (0);
            mPinModify.SetTeoPrologue(0, 0);
            mPinModify.SetTeoPrologue(1, 0);
            mPinModify.SetTeoPrologue(2, 0);
            mPinModify.SetData(pinModifyData, pinModifyData.Length);

            // Initialize modify pin button
            mModifyPinButton = (Button)FindViewById(Resource.Id.main_button_modify_pin);
            mModifyPinButton.Click += delegate(object sender, EventArgs args)
            {
                ShowDialog(DIALOG_MODIFY_PIN_ID);
            };

            // Initialize read key option
            mReadKeyOption.TimeOut = (0);
            mReadKeyOption.PinMaxExtraDigit = (0x0408);
            mReadKeyOption.KeyReturnCondition = (0x01);
            mReadKeyOption.EchoLcdStartPosition = (0);
            mReadKeyOption.EchoLcdMode = (0x01);

            // Initialize read key button
            mReadKeyButton = (Button)FindViewById(Resource.Id.main_button_read_key);
            mReadKeyButton.Click += delegate(object sender, EventArgs args)
            {
                ShowDialog(DIALOG_READ_KEY_ID);
            };

            // Initialize LCD message
            mLcdMessage = "Hello!";

            // Initialize display LCD message button
            mDisplayLcdMessageButton = (Button)FindViewById(Resource.Id.main_button_display_lcd_message);
            mDisplayLcdMessageButton.Click += delegate(object sender, EventArgs args)
            {
                ShowDialog(DIALOG_DISPLAY_LCD_MESSAGE_ID);
            };


            // Disable buttons
            SetButtons(false);

            // Hide input window
            Window.SetSoftInputMode(SoftInput.StateAlwaysHidden);


            /*
FindViewById(Resource.Id
             */

        }

        protected override void OnDestroy()
        {
            mReader.Close();

            UnregisterReceiver(mReceiver);

            base.OnDestroy();
        }

        [System.Obsolete]
        protected override Dialog OnCreateDialog(int id)
        {
            LayoutInflater inflater;
            View layout;
            AlertDialog.Builder builder;
            AlertDialog dialog = null;

            MainActivity a = Application.Context as MainActivity;

            switch (id)
            {
                ///////////

                case DIALOG_VERIFY_PIN_ID:
                    inflater = Application.Context.GetSystemService(Context.LayoutInflaterService) as LayoutInflater;
                    layout = inflater
                            .Inflate(
                                    Resource.Layout.verify_pin_dialog,
                                    (ViewGroup)FindViewById(Resource.Id.verify_pin_dialog_scroll_view));

                    builder = new AlertDialog.Builder(this);
                    builder.SetView(layout);
                    builder.SetTitle("Verify PIN");

                    builder.SetPositiveButton("OK", (senderAlert, args) => {
                        EditText editText;
                        byte[] buffer;

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.verify_pin_dialog_edit_text_timeout);
                        buffer = toByteArray(editText.Text);

                        if (buffer != null && buffer.Length > 0)
                        {
                            mPinVerify.TimeOut = buffer[0] & 0xFF;
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.verify_pin_dialog_edit_text_timeout2);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mPinVerify.TimeOut2 = buffer[0] & 0xFF;
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.verify_pin_dialog_edit_text_format_string);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mPinVerify.FormatString = buffer[0] & 0xFF;
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.verify_pin_dialog_edit_text_pin_block_string);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mPinVerify.PinBlockString = buffer[0] & 0xFF;
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.verify_pin_dialog_edit_text_pin_length_format);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mPinVerify.PinLengthFormat = buffer[0] & 0xFF;
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.verify_pin_dialog_edit_text_pin_max_extra_digit);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 1)
                        {
                            mPinVerify
                                    .PinMaxExtraDigit = ((buffer[0] & 0xFF) << 8
                                            | (buffer[1] & 0xFF));
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.verify_pin_dialog_edit_text_entry_validation_condition);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mPinVerify
                                    .EntryValidationCondition = (buffer[0] & 0xFF);
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.verify_pin_dialog_edit_text_number_message);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mPinVerify.NumberMessage = (buffer[0] & 0xFF);
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.verify_pin_dialog_edit_text_lang_id);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 1)
                        {
                            mPinVerify.LangId = ((buffer[0] & 0xFF) << 8
                                    | (buffer[1] & 0xFF));
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.verify_pin_dialog_edit_text_msg_index);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mPinVerify.MsgIndex = (buffer[0] & 0xFF);
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.verify_pin_dialog_edit_text_teo_prologue);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 2)
                        {
                            mPinVerify.SetTeoPrologue(0, buffer[0] & 0xFF);
                            mPinVerify.SetTeoPrologue(1, buffer[1] & 0xFF);
                            mPinVerify.SetTeoPrologue(2, buffer[2] & 0xFF);
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.verify_pin_dialog_edit_text_data);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mPinVerify.SetData(buffer, buffer.Length);
                        }

                        // Get slot number
                        int slotNum = mSlotSpinner.SelectedItemPosition;


                        // If slot is selected
                        if (slotNum != Spinner.InvalidPosition)
                        {
                            // Set parameters
                            var p = new TransmitParams();
                            TransmitParams Params = new TransmitParams();
                                Params.slotNum = slotNum;
                                Params.controlCode = mFeatures.GetControlCode(Features.FeatureVerifyPinDirect);

                                Params.commandString = toHexString(mPinVerify.ToByteArray());

                            // Transmit control command
                            logMsg("Slot " + slotNum
                                    + ": Verifying PIN (Control Code: "
                                    + Params.controlCode + ")...");
                            new TransmitTask(a).Execute(Params);
                        }

                        dialog.Dismiss();

                    });

                    builder.SetNegativeButton("Cancel", (senderAlert, args) =>
                    {
                        dialog.Cancel();
                    });


                    dialog = builder.Create();


                    // Hide input window
                    dialog.Window.SetSoftInputMode(SoftInput.StateHidden);

                    break;
                case DIALOG_MODIFY_PIN_ID:
                    inflater = Application.Context.GetSystemService(Context.LayoutInflaterService) as LayoutInflater;
                    layout = inflater
                            .Inflate(
                                    Resource.Layout.modify_pin_dialog,
                                    (ViewGroup)FindViewById(Resource.Id.modify_pin_dialog_scroll_view));

                    builder = new AlertDialog.Builder(this);
                    builder.SetView(layout);
                    builder.SetTitle("Modify PIN");

                    builder.SetPositiveButton("OK", (senderAlert, args) => 
                    {
                        EditText editText;
                        byte[] buffer;

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.modify_pin_dialog_edit_text_timeout);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mPinModify.TimeOut = buffer[0] & 0xFF;
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.modify_pin_dialog_edit_text_timeout2);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mPinModify.TimeOut2 = buffer[0] & 0xFF;
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.modify_pin_dialog_edit_text_format_string);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mPinModify.FormatString = buffer[0] & 0xFF;
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.modify_pin_dialog_edit_text_pin_block_string);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mPinModify.PinBlockString = buffer[0] & 0xFF;
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.modify_pin_dialog_edit_text_pin_length_format);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mPinModify.PinLengthFormat = buffer[0] & 0xFF;
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.modify_pin_dialog_edit_text_insertion_offset_old);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mPinModify.InsertionOffsetOld = (buffer[0] & 0xFF);
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.modify_pin_dialog_edit_text_insertion_offset_new);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mPinModify.InsertionOffsetNew = (buffer[0] & 0xFF);
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.modify_pin_dialog_edit_text_pin_max_extra_digit);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 1)
                        {
                            mPinModify
                                    .PinMaxExtraDigit = ((buffer[0] & 0xFF) << 8
                                            | (buffer[1] & 0xFF));
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.modify_pin_dialog_edit_text_confirm_pin);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mPinModify.ConfirmPin = (buffer[0] & 0xFF);
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.modify_pin_dialog_edit_text_entry_validation_condition);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mPinModify
                                    .EntryValidationCondition = (buffer[0] & 0xFF);
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.modify_pin_dialog_edit_text_number_message);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mPinModify.NumberMessage = (buffer[0] & 0xFF);
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.modify_pin_dialog_edit_text_lang_id);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 1)
                        {
                            mPinModify.LangId = ((buffer[0] & 0xFF) << 8
                                    | (buffer[1] & 0xFF));
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.modify_pin_dialog_edit_text_msg_index1);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mPinModify.MsgIndex1 = (buffer[0] & 0xFF);
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.modify_pin_dialog_edit_text_msg_index2);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mPinModify.MsgIndex2 = (buffer[0] & 0xFF);
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.modify_pin_dialog_edit_text_msg_index3);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mPinModify.MsgIndex3 = (buffer[0] & 0xFF);
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.modify_pin_dialog_edit_text_teo_prologue);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 2)
                        {
                            mPinModify.SetTeoPrologue(0, buffer[0] & 0xFF);
                            mPinModify.SetTeoPrologue(1, buffer[1] & 0xFF);
                            mPinModify.SetTeoPrologue(2, buffer[2] & 0xFF);
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.modify_pin_dialog_edit_text_data);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mPinModify.SetData(buffer, buffer.Length);
                        }

                        // Get slot number
                        int slotNum = mSlotSpinner.SelectedItemPosition;

                        // If slot is selected
                        if (slotNum != Spinner.InvalidPosition)
                        {
                            // Set parameters
                            TransmitParams Params = new TransmitParams();
                            Params.slotNum = slotNum;
                            Params.controlCode = mFeatures
                                        .GetControlCode(Features.FeatureModifyPinDirect);
                            Params.commandString = toHexString(mPinModify
                                        .ToByteArray());

                            // Transmit control command
                            logMsg("Slot " + slotNum
                                    + ": Modifying PIN (Control Code: "
                                    + Params.controlCode + ")...");
                            new TransmitTask(a).Execute(Params);
                        }

                        dialog.Dismiss();



                    });

                    builder.SetNegativeButton("Cancel", (senderAlert, args) =>
                    {
                        dialog.Cancel();
                    });


                    dialog = builder.Create();


                    // Hide input window
                    dialog.Window.SetSoftInputMode(SoftInput.StateHidden);

                    break;

                case DIALOG_READ_KEY_ID:
                    inflater = Application.Context.GetSystemService(Context.LayoutInflaterService) as LayoutInflater;
                    layout = inflater
                            .Inflate(
                                    Resource.Layout.read_key_dialog,
                                    (ViewGroup)FindViewById(Resource.Id.read_key_dialog_scroll_view));

                    builder = new AlertDialog.Builder(this);
                    builder.SetView(layout);
                    builder.SetTitle("Read Key");

                    builder.SetPositiveButton("OK", (senderAlert, args) =>
                    {
                        EditText editText;
                        byte[] buffer;

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.read_key_dialog_edit_text_timeout);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mReadKeyOption.TimeOut = buffer[0] & 0xFF;
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.read_key_dialog_edit_text_pin_max_extra_digit);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 1)
                        {
                            mReadKeyOption
                                    .PinMaxExtraDigit = ((buffer[0] & 0xFF) << 8
                                            | (buffer[1] & 0xFF));
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.read_key_dialog_edit_text_key_return_condition);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mReadKeyOption
                                    .KeyReturnCondition = (buffer[0] & 0xFF);
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.read_key_dialog_edit_text_echo_lcd_start_position);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mReadKeyOption
                                    .EchoLcdStartPosition = (buffer[0] & 0xFF);
                        }

                        editText = (EditText)layout
                                .FindViewById(Resource.Id.read_key_dialog_edit_text_echo_lcd_mode);
                        buffer = toByteArray(editText.Text);
                        if (buffer != null && buffer.Length > 0)
                        {
                            mReadKeyOption.EchoLcdMode = (buffer[0] & 0xFF);
                        }

                        // Get slot number
                        int slotNum = mSlotSpinner.SelectedItemPosition;

                        // If slot is selected
                        if (slotNum != Spinner.InvalidPosition)
                        {

                            // Set parameters
                            TransmitParams Params = new TransmitParams();
                            Params.slotNum = slotNum;
                            Params.controlCode = Reader.IoctlAcr83ReadKey;
                            Params.commandString = toHexString(mReadKeyOption
                                        .ToByteArray());

                            // Transmit control command
                            logMsg("Slot " + slotNum
                                    + ": Reading key (Control Code: "
                                    + Params.controlCode + ")...");
                            new TransmitTask(a).Execute(Params);
                        }

                        dialog.Dismiss();

                    });



                    builder.SetNegativeButton("Cancel", (senderAlert, args) =>
                    {
                        dialog.Cancel();
                    });


                    dialog = builder.Create();


                    // Hide input window
                    dialog.Window.SetSoftInputMode(SoftInput.StateHidden);

                    break;
                case DIALOG_DISPLAY_LCD_MESSAGE_ID:
                    inflater = Application.Context.GetSystemService(Context.LayoutInflaterService) as LayoutInflater;
                    layout = inflater
                            .Inflate(
                                    Resource.Layout.display_lcd_message_dialog,
                                    (ViewGroup)FindViewById(Resource.Id.display_lcd_message_dialog_scroll_view));

                    builder = new AlertDialog.Builder(this);
                    builder.SetView(layout);
                    builder.SetTitle("Display LCD Message");

                    builder.SetPositiveButton("OK", (senderAlert, args) => {
                        EditText editText = (EditText)layout
                                .FindViewById(Resource.Id.display_lcd_message_dialog_edit_text_message);
                        mLcdMessage = editText.Text;

                        // Get slot number
                        int slotNum = mSlotSpinner.SelectedItemPosition;

                        // If slot is selected
                        {

                            // Set parameters
                            TransmitParams Params = new TransmitParams();
                            Params.slotNum = slotNum;
                            Params.controlCode = Reader.IoctlAcr83DisplayLcdMessage;
                            try
                            {
                                Params.commandString = toHexString(getBytes(mLcdMessage));
                            }
                            catch (UnsupportedEncodingException e)
                            {
                                Params.commandString = "";
                            }

                            // Transmit control command
                            logMsg("Slot "
                                    + slotNum
                                    + ": Displaying LCD message (Control Code: "
                                    + Params.controlCode + ")...");
                            new TransmitTask(a).Execute(Params);
                        }

                        dialog.Dismiss();
                    });

                    builder.SetNegativeButton("Cancel", (senderAlert, args) =>
                    {
                        dialog.Cancel();
                    });


                    dialog = builder.Create();


                    // Hide input window
                    dialog.Window.SetSoftInputMode(SoftInput.StateHidden);

                    break;

                    /// 
                    /// 
            }

            return base.OnCreateDialog(id);
        }

        [System.Obsolete]
        protected override void OnPrepareDialog(int id, Dialog dialog)
        {
            EditText editText;

            switch (id)
            {

                case DIALOG_VERIFY_PIN_ID:
                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.verify_pin_dialog_edit_text_timeout);
                    editText.Text = (toHexString(mPinVerify.TimeOut));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.verify_pin_dialog_edit_text_timeout2);
                    editText.Text = (toHexString(mPinVerify.TimeOut2));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.verify_pin_dialog_edit_text_format_string);
                    editText.Text = (toHexString(mPinVerify.FormatString));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.verify_pin_dialog_edit_text_pin_block_string);
                    editText.Text = (toHexString(mPinVerify.PinBlockString));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.verify_pin_dialog_edit_text_pin_length_format);
                    editText.Text = (toHexString(mPinVerify.PinLengthFormat));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.verify_pin_dialog_edit_text_pin_max_extra_digit);
                    editText.Text = (toHexString(mPinVerify.PinMaxExtraDigit));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.verify_pin_dialog_edit_text_entry_validation_condition);
                    editText.Text = (toHexString(mPinVerify
                            .EntryValidationCondition));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.verify_pin_dialog_edit_text_number_message);
                    editText.Text = (toHexString(mPinVerify.NumberMessage));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.verify_pin_dialog_edit_text_lang_id);
                    editText.Text = (toHexString(mPinVerify.LangId));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.verify_pin_dialog_edit_text_msg_index);
                    editText.Text = (toHexString(mPinVerify.MsgIndex));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.verify_pin_dialog_edit_text_teo_prologue);
                    editText.Text = (toHexString(mPinVerify.GetTeoPrologue(0)) + " "
                            + toHexString(mPinVerify.GetTeoPrologue(1)) + " "
                            + toHexString(mPinVerify.GetTeoPrologue(2)));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.verify_pin_dialog_edit_text_data);
                    editText.Text = (toHexString(mPinVerify.GetData()));
                    break;

                case DIALOG_MODIFY_PIN_ID:
                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.modify_pin_dialog_edit_text_timeout);
                    editText.Text = (toHexString(mPinModify.TimeOut));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.modify_pin_dialog_edit_text_timeout2);
                    editText.Text = (toHexString(mPinModify.TimeOut2));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.modify_pin_dialog_edit_text_format_string);
                    editText.Text = (toHexString(mPinModify.FormatString));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.modify_pin_dialog_edit_text_pin_block_string);
                    editText.Text = (toHexString(mPinModify.PinBlockString));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.modify_pin_dialog_edit_text_pin_length_format);
                    editText.Text = (toHexString(mPinModify.PinLengthFormat));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.modify_pin_dialog_edit_text_insertion_offset_new);
                    editText.Text = (toHexString(mPinModify.InsertionOffsetNew));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.modify_pin_dialog_edit_text_insertion_offset_old);
                    editText.Text = (toHexString(mPinModify.InsertionOffsetOld));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.modify_pin_dialog_edit_text_pin_max_extra_digit);
                    editText.Text = (toHexString(mPinModify.PinMaxExtraDigit));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.modify_pin_dialog_edit_text_confirm_pin);
                    editText.Text = (toHexString(mPinModify.ConfirmPin));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.modify_pin_dialog_edit_text_entry_validation_condition);
                    editText.Text = (toHexString(mPinModify
                            .EntryValidationCondition));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.modify_pin_dialog_edit_text_number_message);
                    editText.Text = (toHexString(mPinModify.NumberMessage));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.modify_pin_dialog_edit_text_lang_id);
                    editText.Text = (toHexString(mPinModify.LangId));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.modify_pin_dialog_edit_text_msg_index1);
                    editText.Text = (toHexString(mPinModify.MsgIndex1));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.modify_pin_dialog_edit_text_msg_index2);
                    editText.Text = (toHexString(mPinModify.MsgIndex2));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.modify_pin_dialog_edit_text_msg_index3);
                    editText.Text = (toHexString(mPinModify.MsgIndex3));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.modify_pin_dialog_edit_text_teo_prologue);
                    editText.Text = (toHexString(mPinModify.GetTeoPrologue(0)) + " "
                            + toHexString(mPinModify.GetTeoPrologue(1)) + " "
                            + toHexString(mPinModify.GetTeoPrologue(2)));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.modify_pin_dialog_edit_text_data);
                    editText.Text = (toHexString(mPinModify.GetData()));
                    break;

                case DIALOG_READ_KEY_ID:
                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.read_key_dialog_edit_text_timeout);
                    editText.Text = (toHexString(mReadKeyOption.TimeOut));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.read_key_dialog_edit_text_pin_max_extra_digit);
                    editText.Text = (toHexString(mReadKeyOption.PinMaxExtraDigit));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.read_key_dialog_edit_text_key_return_condition);
                    editText.Text = (toHexString(mReadKeyOption.KeyReturnCondition));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.read_key_dialog_edit_text_echo_lcd_start_position);
                    editText.Text = (toHexString(mReadKeyOption
                            .EchoLcdStartPosition));

                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.read_key_dialog_edit_text_echo_lcd_mode);
                    editText.Text = (toHexString(mReadKeyOption.EchoLcdMode));
                    break;

                case DIALOG_DISPLAY_LCD_MESSAGE_ID:
                    editText = (EditText)dialog
                            .FindViewById(Resource.Id.display_lcd_message_dialog_edit_text_message);
                    editText.Text = (mLcdMessage);
                    break;

                default:
                    break;
            }

        }

        private void MReader_StateChange(object sender, Reader.StateChangeEventArgs e)
        {
            OnStateChange(e.P0, e.P1, e.P2);
        }
        public void OnStateChange(int slotNum, int prevState, int currState)
        {

            if (prevState < Reader.CardUnknown
                    || prevState > Reader.CardSpecific)
            {
                prevState = Reader.CardSpecific;
            }

            if (currState < Reader.CardUnknown
                    || currState > Reader.CardSpecific)
            {
                currState = Reader.CardUnknown;
            }

            // Create output string
            string outputString = "Slot " + slotNum + ": "
                    + stateStrings[prevState] + " -> "
                    + stateStrings[currState];

            // Show output

            RunOnUiThread(() =>
            {
                logMsg(outputString);
            });

      }

        public void logMsg(string msg)
        {
            DateTime date = DateTime.Now;
            string oldMsg = mResponseTextView.Text;

            mResponseTextView.Text = oldMsg + "\n" + date.ToString("yyyy-MM-dd HH':'mm':'ss") + msg;

            var lc = mResponseTextView.LineCount;

            if (lc > MAX_LINES)
            {
                mResponseTextView.ScrollTo(0, (lc - MAX_LINES) * mResponseTextView.LineHeight);
            }
        }

        public void logBuffer(byte[] buffer, int bufferLength)
        {

            string bufferString = "";

            for (int i = 0; i < bufferLength; i++)
            {

                string hexChar = string.Format("0x{0:X}", buffer[i] & 0xFF);

                if (i % 16 == 0)
                {

                    if (bufferString != "")
                    {

                        logMsg(bufferString);
                        bufferString = "";
                    }
                }

                bufferString += hexChar.ToUpper() + " ";
            }

            if (bufferString != "")
            {
                logMsg(bufferString);
            }
        }

        public byte[] toByteArray(string hexString)
        {

            int hexStringLength = hexString.Length;
            byte[] byteArray = null;
            int count = 0;
            char c;
            int i;

            // Count number of hex characters
            for (i = 0; i < hexStringLength; i++)
            {

                c = hexString[i];
                if (c >= '0' && c <= '9' || c >= 'A' && c <= 'F' || c >= 'a'
                        && c <= 'f')
                {
                    count++;
                }
            }

            byteArray = new byte[(count + 1) / 2];
            var first = true;
            int len = 0;
            int value;
            for (i = 0; i < hexStringLength; i++)
            {

                c = hexString[i];
                if (c >= '0' && c <= '9')
                {
                    value = c - '0';
                }
                else if (c >= 'A' && c <= 'F')
                {
                    value = c - 'A' + 10;
                }
                else if (c >= 'a' && c <= 'f')
                {
                    value = c - 'a' + 10;
                }
                else
                {
                    value = -1;
                }

                if (value >= 0)
                {
                    if (first)
                    {
                        byteArray[len] = (byte)(value << 4);
                    }
                    else
                    {
                        byteArray[len] |= (byte)value;
                        len++;
                    }

                    first = !first;
                }
            }

            return byteArray;
        }

        /**
         * Converts the integer to HEX string.
         * 
         * @param i
         *            the integer.
         * @return the HEX string.
         */
        private string toHexString(int i)
        {
            return $"0x{i:X}";
        }
        private string toHexString(byte[] buffer)
        {
            return $"0x{buffer:X}";
        }
        private byte[] getBytes(string hash)
        {
            System.Text.ASCIIEncoding encoding = new System.Text.ASCIIEncoding();
            return encoding.GetBytes(hash);
        }
    }
}

