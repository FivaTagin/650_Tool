using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using System.Timers;
using CyUSB;

namespace CyControl
{
    public partial class Form1 : Form
    {
        CyFX2Device fx2;
        USBDeviceList usbDevices;
        CyUSBEndPoint curEndpt;

        CyUSBDevice curCyUsbDev;
        CyHidDevice curHidDev;
        CyHidReport curHidReport;

        string dataCaption;
        string scriptfile;
        string playscriptfile;
        string monitorfile;
        string fname;
        string Datastring;
        static int file_bytes;
        bool bRecording;
        byte[] file_buffer;

        ArrayList list;
        ArrayList list1;

        TTransaction Xaction;
        FileStream stream;
        StreamWriter sw;
        FileStream script_stream;

        byte Reqcode;
        ushort wvalue;
        ushort windex;

        ushort Resetreg;
        ushort Maxaddr;
        int Sync_Form_Resize = 0;
        long Max_Ctlxfer_size;

        //
        // debug tool global values.
        //
        private static System.Timers.Timer gTimer;
        String strCurrentPrecision;
        byte[] bufferByteData = new byte[1024];
        Form2 gf2;
        CyUSBEndPoint bulkReadTag;
        int gCntFramePackage;
        bool gflagAutoFrame;
        string fileName = @"Frame.txt";
        


        /* Summary
            Main entry to the application through Constructor
        */
        public Form1()
        {
            double tempData = 0.0;
            int tempByte = -100;

            tempByte = (0xff << 24) | (0xff << 16) | (0xa7 << 8 ) | (0x00);
            tempData = tempByte / 150000000.0;
            

            Initialize();

            //Initializes form resources
            InitializeComponent();

            //Set the customer class GUID(vendor specific) and Driver Guid(vendor specific)
            //CyConst.SetCustomerGUID("{CDBF8987-75F1-468e-8217-97197F88F773}", "{C955D74D-0430-44f2-B120-276D94492D2D}");
            CyConst.SetClassGuid("{CDBF8987-75F1-468e-8217-97197F88F773}");

            // This call instantiates usbDevices per the driver classes selected
            CyUSBDeviceBox_CheckedChanged(this, null);

            Sync_Form_Resize = 1;
            Form1_Resize(this, null);

            gf2 = new Form2(this);

            OutputBox.Text = "650 Test Tool Init. Test Data: ";
            OutputBox.Text += tempData.ToString() + "\r\n";

        }

        /* Summary
            Initialize global variables defined
        */
        private void Initialize()
        {
            scriptfile = "";
            playscriptfile = "";
            monitorfile = "";
            Resetreg = 0xE600;
            Maxaddr = 0x4000;
            Max_Ctlxfer_size = 0x1000;
            bRecording = false;
            Xaction = new TTransaction();
            list = new ArrayList();
            list1 = new ArrayList();

            curEndpt = null;
            curCyUsbDev = null;
            curHidDev = null;
            curHidReport = null;
            bulkReadTag = null;
            gCntFramePackage = (int)Precision.x1;
            gflagAutoFrame = false;



            // initial timer callbacks
            // Create a timer with a ten second interval.
            gTimer = new System.Timers.Timer(1000);
            // Hook up the Elapsed event for the timer.
            gTimer.Elapsed += funcCallbackAutoReadFrame;
            gTimer.AutoReset = false;
            gTimer.Start();


        }

        /* Summary
            Called while closing the form for Garbage collection
        */
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (usbDevices != null)
            {
                usbDevices.DeviceRemoved -= usbDevices_DeviceRemoved;
                usbDevices.DeviceAttached -= usbDevices_DeviceAttached;
                usbDevices.Dispose();
            }
        }

        /* Summary
            Refreshes the Device tree of control center to update the recent changes like device removal, new device
        */
        private void RefreshDeviceTree()
        {
            DeviceTreeView.Nodes.Clear();
            DescText.Text = "";
            
            foreach (USBDevice dev in usbDevices)
            {
                DeviceTreeView.Nodes.Add(dev.Tree);
            }
                
                

        }

        /* Summary
            Any selection changes in the tree view will trigger this function "at start the first device is always selected leading into this function"
        */
        private void DeviceTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            DeviceTreeView.ExpandAll();
            XferTextBox.Text = "";
            XferDataBox.Text = "";

            TreeNode selNode = DeviceTreeView.SelectedNode;
            string nodeText = selNode.Text;

            CyUSBInterfaceContainer curIntfcContainer = selNode.Tag as CyUSBInterfaceContainer;
            CyUSBInterface curIntfc = selNode.Tag as CyUSBInterface;
            CyUSBConfig curConfig = selNode.Tag as CyUSBConfig;

            curEndpt = selNode.Tag as CyUSBEndPoint;
            curCyUsbDev = selNode.Tag as CyUSBDevice;

            // store bulk read device information.
            if (curEndpt != null && curEndpt.ToString().Contains("86h"))
                bulkReadTag = curEndpt;

            curHidDev = null;
            curHidReport = null;

            if (curConfig != null)
            {
                curCyUsbDev = selNode.Parent.Tag as CyUSBDevice;
            }
            else if (curIntfcContainer != null)
            {
                curCyUsbDev = selNode.Parent.Parent.Tag as CyUSBDevice;
            }
            else if (curIntfc != null)
            {
                curCyUsbDev = selNode.Parent.Parent.Parent.Tag as CyUSBDevice;
                curCyUsbDev.AltIntfc = curIntfc.bAlternateSetting;
                if (curCyUsbDev.AltIntfc != curIntfc.bAlternateSetting)
                {
                    //set interface failed
                    MessageBox.Show("Please select another alternate interface.", "Select Alternate Interface Failed");
                }
            }
            else if (curEndpt != null)
            {
                int minXfer = curEndpt.MaxPktSize;
                if (curEndpt.Attributes == 1) minXfer *= 8;
                NumBytesBox.Text = minXfer.ToString();

                // Set the AltSetting
                if (curEndpt.Address != 0) // Only if we're not on the Control Endpoint
                {
                    curCyUsbDev = selNode.Parent.Parent.Parent.Parent.Tag as CyUSBDevice;
                    curIntfc = selNode.Parent.Tag as CyUSBInterface;
                    curCyUsbDev.AltIntfc = curIntfc.bAlternateSetting;
                    if (curCyUsbDev.AltIntfc != curIntfc.bAlternateSetting)
                    {                        
                        MessageBox.Show("Please select another alternate interface.", "Select Alternate Interface Failed");
                        return;
                    }
                }
                else
                {
                    curCyUsbDev = selNode.Parent.Parent.Tag as CyUSBDevice;
                }
            }
            else if ((selNode.Tag is CyHidButton) || (selNode.Tag is CyHidValue))
            {
                curHidDev = selNode.Parent.Parent.Tag as CyHidDevice;
                curHidReport = selNode.Parent.Tag as CyHidReport;

                NumBytesBox.Text = curHidReport.RptByteLen.ToString();
                nodeText = selNode.Parent.Text;
            }
            else if (selNode.Tag is CyHidReport)
            {
                curHidDev = selNode.Parent.Tag as CyHidDevice;
                curHidReport = selNode.Tag as CyHidReport;

                NumBytesBox.Text = curHidReport.RptByteLen.ToString();
            }
            else if (selNode.Tag is CyHidDevice)
                curHidDev = selNode.Tag as CyHidDevice;

            ConfigDataXferBtn(nodeText);

            DescText.Text = selNode.Tag.ToString();

            Sync_Form_Resize = 1;
            Form1_Resize(sender, null);
        }

        /* Summary
            Transfer file button should be visible only for certain conditions which is done here.
        */
        private void ConfigDataXferBtn(string nodeTxt)
        {
            FileXferBtn.Visible = false;
            FileXferBtn.Enabled = false;

            XferTextBox.Enabled = true;
            XferDataBox.Enabled = true;

            //DataXferBtn.Enabled = true;

            //Changed here
            fx2 = Fx2DeviceSelected_forusage();
            if (fx2 != null)
            {
                FileXferBtn.Visible = true;
                FileXferBtn.Enabled = true;
            }
            //
            if (nodeTxt.Contains("Feature"))
            {
                DataXferBtn.Text = "Get Feature";
                FileXferBtn.Text = "Set Feature";
                FileXferBtn.Visible = true;
                FileXferBtn.Enabled = true;
            }
            else if (nodeTxt.Contains("Input"))
            {
                DataXferBtn.Text = "Get Input";

                string os = GetOSName();
                
                //GetInput is only supported under WindowsXP and newer.
                if (!((os == "Windows XP") || (os == "Windows Vista")))
                    DataXferBtn.Enabled = false;
            }
            else if (nodeTxt.Contains("Output"))
                DataXferBtn.Text = "Set Output";
            else
            {
                if (curEndpt != null)
                {
                    if (curEndpt.Attributes != 0)
                    {
                        if (curEndpt.bIn)
                        {
                            DataXferBtn.Text = "Transfer Data-IN";
                            FileXferBtn.Text = "Transfer File-IN";
                        }
                        else
                        {
                            DataXferBtn.Text = "Transfer Data-OUT";
                            FileXferBtn.Text = "Transfer File-OUT";
                        }
                    }
                    else
                    {
                        DataXferBtn.Text = "Transfer Data";
                        FileXferBtn.Text = "Transfer File";
                    }
                }
                else
                {
                    DataXferBtn.Text = "Transfer Data";
                    FileXferBtn.Text = "Transfer File";
                }

            }
            if (curHidDev != null)
            {
                TreeNode selNode = DeviceTreeView.SelectedNode;
                DataXferBtn.Enabled = curHidDev.RwAccessible;

                string os = GetOSName();
                XferTextBox.Enabled = false;
                XferDataBox.Enabled = false;
                //GetInput is only supported under WindowsXP and newer.
                if (!((os == "Windows XP") || (os == "Windows Vista")))
                 DataXferBtn.Enabled = false;
            }
            else
                DataXferBtn.Enabled = true;

        }

        /* Summary
            Event handler of About Menu Item
        */
        private void AboutMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show(Util.Assemblies + "\nCopyright (c) Cypress Semiconductor 2011", Text);
        }

        /* Summary
            For HID devices called from transfer data button and transfer file button
        */
        private void DoHidXfer(object sender, EventArgs e)
        {
            bool bResult = false;

            if (DataXferBtn.Text.Contains("Feature"))
            {
                if (sender == FileXferBtn)
                {
                    dataCaption = "Set feature ";
                    OutputBox.Text += dataCaption;

                    LoadHidReport();
                    bResult = curHidDev.SetFeature(curHidReport.ID);
                }
                else
                {
                    dataCaption = "Get feature ";
                    OutputBox.Text += dataCaption;
                    bResult = curHidDev.GetFeature(curHidReport.ID);
                }
            }

            else if (DataXferBtn.Text.Contains("Input"))
            {
                dataCaption = "Get input ";
                OutputBox.Text += dataCaption;
                bResult = curHidDev.GetInput(curHidReport.ID);
            }

            else if (DataXferBtn.Text.Contains("Output"))
            {
                dataCaption = "Set output ";
                OutputBox.Text += dataCaption;

                LoadHidReport();
                bResult = curHidDev.SetOutput(curHidReport.ID);
            }

            if (bResult)
                DisplayXferData(curHidReport.DataBuf, curHidReport.RptByteLen, true);
            else
            {
                OutputBox.Text += string.Format("\r\n{0}failed\r\n\r\n", dataCaption);
                OutputBox.SelectionStart = OutputBox.Text.Length;
                OutputBox.ScrollToCaret();
            }
        }

        /* Summary
            Called from DoHidXfer
        */
        private void LoadHidReport()
        {
            if (curHidReport == null) return;

            curHidReport.Clear();

            // Load the report buffer with the hex bytes in XferDataBox
            string[] separators = { " " };
            string[] hexVals = XferDataBox.Text.Split(separators, StringSplitOptions.RemoveEmptyEntries);

            int i = 1;
            foreach (string s in hexVals)
                if (i <= curHidReport.RptByteLen)
                    curHidReport.DataBuf[i++] = (byte)Convert.ToInt32(s, 16);

        }

        /* Summary
            Event handler for Transfer File button
        */
        private void FileXferBtn_Click(object sender, EventArgs e)
        {
            if (curHidReport != null)
            {
                DoHidXfer(sender, e);
                return;
            }

            //Code added to transfer small file: no buffering, only one transfer
            if (curEndpt == null)
            {
                MessageBox.Show("Select <bulk> <iso> <int> endpoint enabled in the device tree.", "No endpoint selected");
                return;
            }

            long flen = 0;

            string fname = "";

            if (bRecording && (script_stream != null))
            {
                Xaction.ConfigNum = fx2.Config;
                Xaction.IntfcNum = 0;
                Xaction.AltIntfc = fx2.AltIntfc;
                Xaction.EndPtAddr = curEndpt.Address;
                // Set the Tag (file xfer) to 1 only if reading data from the device
                Xaction.Tag = curEndpt.bIn ? (byte)1 : (byte)0;
            }

            bool success = false;

            switch (curEndpt.Attributes)
            {
                case 0:
                    CyControlEndPoint ctrlEpt = curEndpt as CyControlEndPoint;

                    if (TargetBox.Text.Equals("Device")) ctrlEpt.Target = CyConst.TGT_DEVICE;
                    else if (TargetBox.Text.Equals("Interface")) ctrlEpt.Target = CyConst.TGT_INTFC;
                    else if (TargetBox.Text.Equals("Endpoint")) ctrlEpt.Target = CyConst.TGT_ENDPT;
                    else if (TargetBox.Text.Equals("Other")) ctrlEpt.Target = CyConst.TGT_OTHER;

                    if (ReqTypeBox.Text.Equals("Standard")) ctrlEpt.ReqType = CyConst.REQ_STD;
                    else if (ReqTypeBox.Text.Equals("Class")) ctrlEpt.ReqType = CyConst.REQ_CLASS;
                    else if (ReqTypeBox.Text.Equals("Vendor")) ctrlEpt.ReqType = CyConst.REQ_VENDOR;

                    ctrlEpt.Direction = DirectionBox.Text.Equals("In") ? CyConst.DIR_FROM_DEVICE : CyConst.DIR_TO_DEVICE;

                    try
                    {
                        ctrlEpt.ReqCode = (byte)Convert.ToInt16(ReqCodeBox.Text, 16); //(byte)Util.HexToInt(ReqCodeBox.Text);
                        ctrlEpt.Value = (ushort)Convert.ToInt16(wValueBox.Text, 16); //(ushort)Util.HexToInt(wValueBox.Text);
                        ctrlEpt.Index = (ushort)Convert.ToInt16(wIndexBox.Text, 16); //(ushort)Util.HexToInt(wIndexBox.Text);
                    }
                    catch (Exception exc)
                    {
                        MessageBox.Show(exc.Message, "Input Error");
                        return;
                    }

                    if (ctrlEpt.Direction == CyConst.DIR_TO_DEVICE)
                    {
                        string tmpFilter = FOpenDialog.Filter;
                        string title = FOpenDialog.Title;
                        FOpenDialog.Title = "Select the file to send";
                        FOpenDialog.Filter = "All files (*.*) | *.*";

                        if (FOpenDialog.ShowDialog() == DialogResult.OK)
                        {
                            fname = FOpenDialog.FileName;
                            StatLabel.Text = "File selected.... " + FOpenDialog.FileName;
                            Refresh();
                        }
                        else
                        {
                            FOpenDialog.Filter = tmpFilter;
                            FOpenDialog.Title = title;
                            return;
                        }

                        FOpenDialog.FileName = "";
                        FOpenDialog.Filter = tmpFilter;
                        FOpenDialog.Title = title;

                        FileStream file = new FileStream(fname, FileMode.Open, FileAccess.Read);
                        flen = file.Length;
                        //file_bytes = (int)flen;

                        file_bytes = Convert.ToInt32(NumBytesBox.Text);
                        file_buffer = new byte[file_bytes];

                        file.Read(file_buffer, 0, file_bytes);
                        file.Close();

                        curEndpt.XferSize = Convert.ToInt32(NumBytesBox.Text);
                        success = ctrlEpt.XferData(ref file_buffer, ref file_bytes);

                        if (bRecording && (script_stream != null))
                        {
                            Xaction.Tag = 0;

                            Xaction.bReqType = (byte)(ctrlEpt.Direction | ctrlEpt.ReqType | ctrlEpt.Target);
                            Xaction.CtlReqCode = ctrlEpt.ReqCode;
                            Xaction.wValue = ctrlEpt.Value;
                            Xaction.wIndex = ctrlEpt.Index;

                            Xaction.DataLen = (uint)file_bytes;
                            Xaction.Timeout = ctrlEpt.TimeOut / 1000;
                            Xaction.RecordSize = (uint)file_bytes + TTransaction.TotalHeaderSize;

                            //Write xaction and buffer
                            Xaction.WriteToStream(script_stream);
                            Xaction.WriteFromBuffer(script_stream, ref file_buffer, ref file_bytes);
                        }

                        BuildDataCaption();
                        OutputBox.Text += dataCaption;
                        DisplayXferData(file_buffer, file_bytes, success);
                    }
                    else
                    {
                        file_bytes = Convert.ToInt32(NumBytesBox.Text);
                        byte[] To_file = new byte[file_bytes];

                        curEndpt.XferSize = Convert.ToInt32(NumBytesBox.Text);
                        success = ctrlEpt.XferData(ref To_file, ref file_bytes);
                        if (success)
                        {
                            string filename;

                            string tmpFilter = FSave.Filter;
                            string title = FSave.Title;
                            FSave.Title = "Save Data to file:";
                            FSave.Filter = "All files (*.*) | *.*";

                            if (FSave.ShowDialog() == DialogResult.OK)
                            {
                                filename = FSave.FileName;
                                Refresh();

                                FSave.Title = title;
                                FSave.Filter = tmpFilter;

                                try
                                {
                                    FileStream file = new FileStream(filename, FileMode.Create);
                                    file.Write(To_file, 0, file_bytes);
                                    file.Close();
                                }
                                catch (Exception exc)
                                {
                                    MessageBox.Show(exc.Message, "Check the File Attributes");
                                }
                            }
                            else
                            {
                                FSave.Title = title;
                                FSave.Filter = tmpFilter;
                                return;
                            }

                            FSave.FileName = "";
                            FSave.Title = title;
                            FSave.Filter = tmpFilter;
                        }

                        if (bRecording && (script_stream != null))
                        {
                            Xaction.Tag = 1;

                            Xaction.bReqType = (byte)(ctrlEpt.Direction | ctrlEpt.ReqType | ctrlEpt.Target);
                            Xaction.CtlReqCode = ctrlEpt.ReqCode;
                            Xaction.wValue = ctrlEpt.Value;
                            Xaction.wIndex = ctrlEpt.Index;

                            Xaction.DataLen = (uint)file_bytes;
                            Xaction.RecordSize = TTransaction.TotalHeaderSize;  // Don't save the data in script

                            //Write xaction
                            Xaction.WriteToStream(script_stream);
                        }

                        BuildDataCaption();
                        OutputBox.Text += dataCaption;
                        DisplayXferData(To_file, file_bytes, success);
                    }
                    break;
                default:
                    if (!curEndpt.bIn)
                    {
                        string tmpFilter = FOpenDialog.Filter;
                        string title = FOpenDialog.Title;
                        FOpenDialog.Title = "Select the file to send";
                        FOpenDialog.Filter = "All files (*.*) | *.*";

                        if (FOpenDialog.ShowDialog() == DialogResult.OK)
                        {
                            fname = FOpenDialog.FileName;
                            StatLabel.Text = "File selected.... " + FOpenDialog.FileName;
                            Refresh();
                        }
                        else
                        {
                            FOpenDialog.Filter = tmpFilter;
                            FOpenDialog.Title = title;
                            return;
                        }

                        FOpenDialog.FileName = "";
                        FOpenDialog.Filter = tmpFilter;
                        FOpenDialog.Title = title;

                        FileStream file = new FileStream(fname, FileMode.Open, FileAccess.Read);
                        flen = file.Length;
                        file_bytes = (int)flen;

                        //file_bytes = Convert.ToInt32(NumBytesBox.Text); // File-OUT transfer it will take file lenght as a number of bytes to transfer.
                        file_buffer = new byte[file_bytes];

                        file.Read(file_buffer, 0, file_bytes);
                        file.Close();

                        //curEndpt.XferSize = Convert.ToInt32(NumBytesBox.Text);//
                        curEndpt.XferSize = file_bytes;
                        success = curEndpt.XferData(ref file_buffer, ref file_bytes);

                        if (bRecording && (script_stream != null))
                        {
                            Xaction.DataLen = (uint)file_bytes;
                            Xaction.Timeout = curEndpt.TimeOut / 1000;
                            Xaction.RecordSize = (uint)file_bytes + TTransaction.TotalHeaderSize;

                            //Write xaction and buffer
                            Xaction.WriteToStream(script_stream);
                            Xaction.WriteFromBuffer(script_stream, ref file_buffer, ref file_bytes);
                        }

                        BuildDataCaption();
                        OutputBox.Text += dataCaption;
                        DisplayXferData(file_buffer, file_bytes, success);
                    }
                    else
                    {
                        if (NumBytesBox.Text == "")
                        {
                            MessageBox.Show("Please enter Number of Bytes to receive in Bytes to Transfer box.", "Invalid Input");
                            return;
                        }
                        
                        file_bytes = Convert.ToInt32(NumBytesBox.Text);
                        byte[] To_file = new byte[file_bytes];

                        curEndpt.XferSize = Convert.ToInt32(NumBytesBox.Text);
                        success = curEndpt.XferData(ref To_file, ref file_bytes);
                        if (success)
                        {
                            string filename;

                            string tmpFilter = FSave.Filter;
                            string title = FSave.Title;
                            FSave.Title = "Save Data to file:";
                            FSave.Filter = "All files (*.*) | *.*";

                            if (FSave.ShowDialog() == DialogResult.OK)
                            {
                                filename = FSave.FileName;
                                Refresh();

                                FSave.Title = title;
                                FSave.Filter = tmpFilter;

                                try
                                {
                                    FileStream file = new FileStream(filename, FileMode.Create);
                                    file.Write(To_file, 0, file_bytes);
                                    file.Close();
                                }
                                catch (Exception exc)
                                {
                                    MessageBox.Show(exc.Message, "Check the File Attributes");
                                }
                            }
                            else
                            {
                                FSave.Title = title;
                                FSave.Filter = tmpFilter;
                                return;
                            }

                            FSave.FileName = "";
                            FSave.Title = title;
                            FSave.Filter = tmpFilter;
                        }

                        if (bRecording && (script_stream != null))
                        {
                            Xaction.DataLen = (uint)file_bytes;
                            Xaction.RecordSize = TTransaction.TotalHeaderSize;  // Don't save the data in script

                            //Write xaction
                            Xaction.WriteToStream(script_stream);
                        }

                        BuildDataCaption();
                        OutputBox.Text += dataCaption;
                        DisplayXferData(To_file, file_bytes, success);
                    }
                    break;
            }

        }

        /* Summary
            Event handler for Transfer Data button
        */
        private void DataXferBtn_Click(object sender, EventArgs e)
        {
            if (curHidReport != null)
            {
                DoHidXfer(sender, e);
                return;
            }

            if (curHidDev != null)
            {
                MessageBox.Show("Select a HID feature, input or output in the device tree.", "No report selected");
                return;
            }

            if (curEndpt == null)
            {
                MessageBox.Show("Select <bulk> <iso> <int> endpoint enabled in the device tree.", "No endpoint selected");
                return;
            }

            int bytes = 0;

            try
            {
                bytes = Convert.ToInt32(NumBytesBox.Text);
            }
            catch (Exception exc)
            {
                if (bytes < 1)
                {
                    //Just to remove warning
                    exc.ToString();

                    MessageBox.Show("Enter a valid number of bytes to transfer.", "Invalid Byte Count");
                    return;
                }
            }

            byte[] buffer = new byte[bytes];
            bool bXferCompleted = false;

            // Setting control endpt direction needs to occur before BuildDataCaption call
            CyControlEndPoint ctrlEpt = curEndpt as CyControlEndPoint;
            if (ctrlEpt != null)
                ctrlEpt.Direction = DirectionBox.Text.Equals("In") ? CyConst.DIR_FROM_DEVICE : CyConst.DIR_TO_DEVICE;

            // Stuff the output buffer
            if (!curEndpt.bIn)
            {
                string[] hexTokens = XferDataBox.Text.Split(' ');

                int i = 0;

                //foreach (string tok in hexTokens)
                for (int j = 0; j < bytes; j++)
                {
                    string tok;

                    try
                    {
                        tok = hexTokens[j];
                    }
                    catch
                    {
                        tok = "";
                    }
 
                    if (tok.Length > 0)
                    {
                        try
                        {
                            buffer[i++] = (byte)Convert.ToInt32(tok, 16);
                        }
                        catch (Exception exc)
                        {
                            MessageBox.Show(exc.Message, "Input Error");
                            return;
                        }
                    }
                }
            }

            BuildDataCaption();
            OutputBox.Text += dataCaption;
            OutputBox.SelectionStart = OutputBox.Text.Length;
            OutputBox.ScrollToCaret();

            curEndpt.TimeOut = 2000;

            if (ctrlEpt != null)
            {
                if (TargetBox.Text.Equals("Device")) ctrlEpt.Target = CyConst.TGT_DEVICE;
                else if (TargetBox.Text.Equals("Interface")) ctrlEpt.Target = CyConst.TGT_INTFC;
                else if (TargetBox.Text.Equals("Endpoint")) ctrlEpt.Target = CyConst.TGT_ENDPT;
                else if (TargetBox.Text.Equals("Other")) ctrlEpt.Target = CyConst.TGT_OTHER;

                if (ReqTypeBox.Text.Equals("Standard")) ctrlEpt.ReqType = CyConst.REQ_STD;
                else if (ReqTypeBox.Text.Equals("Class")) ctrlEpt.ReqType = CyConst.REQ_CLASS;
                else if (ReqTypeBox.Text.Equals("Vendor")) ctrlEpt.ReqType = CyConst.REQ_VENDOR;

                ctrlEpt.Direction = DirectionBox.Text.Equals("In") ? CyConst.DIR_FROM_DEVICE : CyConst.DIR_TO_DEVICE;

                try
                {
                    ctrlEpt.ReqCode = (byte)Convert.ToInt16(ReqCodeBox.Text, 16); //(byte)Util.HexToInt(ReqCodeBox.Text);
                    ctrlEpt.Value = (ushort)Convert.ToInt16(wValueBox.Text, 16); //(ushort)Util.HexToInt(wValueBox.Text);
                    ctrlEpt.Index = (ushort)Convert.ToInt16(wIndexBox.Text, 16); //(ushort)Util.HexToInt(wIndexBox.Text);
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message, "Input Error");
                    return;
                }

                bXferCompleted = ctrlEpt.XferData(ref buffer, ref bytes);


                if (bRecording && (script_stream != null))
                {
                    Xaction.ConfigNum = fx2.Config;
                    Xaction.IntfcNum = 0;
                    Xaction.AltIntfc = fx2.AltIntfc;
                    Xaction.EndPtAddr = ctrlEpt.Address;
                    Xaction.Tag = 0;

                    Xaction.bReqType = (byte)(ctrlEpt.Direction | ctrlEpt.ReqType | ctrlEpt.Target);
                    Xaction.CtlReqCode = ctrlEpt.ReqCode;
                    Xaction.wValue = ctrlEpt.Value;
                    Xaction.wIndex = ctrlEpt.Index;
                    Xaction.DataLen = (uint)bytes;
                    Xaction.Timeout = ctrlEpt.TimeOut / 1000;
                    Xaction.RecordSize = (uint)bytes + TTransaction.TotalHeaderSize;

                    //Write xaction and buffer
                    Xaction.WriteToStream(script_stream);
                    Xaction.WriteFromBuffer(script_stream, ref buffer, ref bytes);
                }
            }

            bool IsPkt = IsPacket.Checked ? true : false;

            CyBulkEndPoint bulkEpt = curEndpt as CyBulkEndPoint;
            if (bulkEpt != null)
            {
                bXferCompleted = bulkEpt.XferData(ref buffer, ref bytes, IsPkt);
                CheckForScripting(ref buffer, ref bytes);
            }

            CyIsocEndPoint isocEpt = curEndpt as CyIsocEndPoint;
            if (isocEpt != null)
            {
                isocEpt.XferSize = Convert.ToInt32(NumBytesBox.Text);

                int pkts = bytes / isocEpt.MaxPktSize;
                if ((bytes % isocEpt.MaxPktSize) > 0) pkts++;
			    ISO_PKT_INFO[] Iskpt = new ISO_PKT_INFO[pkts];                 
                bXferCompleted = isocEpt.XferData(ref buffer, ref bytes, ref Iskpt);
                if (bXferCompleted)
                {
                    int MainBufOffset = 0;
                    int tmpBufOffset = 0;
                    byte[] tmpbuf = new byte[bytes];
                    // Check all packets and  if Iso in/out packet is not succeeded then don't update the buffer.
                    for (int i = 0; i < pkts; i++)
                    {
                        if (Iskpt[i].Status != 0)
                        {
                            //updated the buffer based on the status of the packets
                            //skip that buffer
                        }
                        else
                        {
                            for (int j = 0; j < Iskpt[i].Length; j++)
                            {
                                tmpbuf[tmpBufOffset] = buffer[MainBufOffset + j]; // get the received/transfered data in the temparary buffer
                                tmpBufOffset++;
                            }
                        }
                        MainBufOffset += isocEpt.MaxPktSize;
                    }
                    // Now copy the temparary buffer to main buffer to display
                    for (int x = 0; x < tmpBufOffset; x++)
                    {
                        buffer[x] = tmpbuf[x]; // Updated the main buffer with the whatever data has been received / transfered.
                    }
                }
                //bXferCompleted = isocEpt.XferData(ref buffer, ref bytes);                
                CheckForScripting(ref buffer, ref bytes);
            }

            CyInterruptEndPoint intEpt = curEndpt as CyInterruptEndPoint;
            if (intEpt != null)
            {
                bXferCompleted = intEpt.XferData(ref buffer, ref bytes, IsPkt);
                CheckForScripting(ref buffer, ref bytes);
            }

            DisplayXferData(buffer, bytes, bXferCompleted);
        }

        private void CheckForScripting(ref byte[] buffer, ref int bytes)
        {
            if (bRecording && (script_stream != null))
            {
                Xaction.ConfigNum = fx2.Config;
                Xaction.IntfcNum = 0;
                Xaction.AltIntfc = fx2.AltIntfc;
                Xaction.EndPtAddr = curEndpt.Address;

                Xaction.Tag = 0;
                Xaction.DataLen = (uint)bytes;
                Xaction.Timeout = curEndpt.TimeOut / 1000;
                Xaction.RecordSize = TTransaction.TotalHeaderSize + (uint)bytes;

                //Write xaction and buffer
                Xaction.WriteToStream(script_stream);
                Xaction.WriteFromBuffer(script_stream, ref buffer, ref bytes);
            }
        }

        /* Summary
            Creates captions to display in the output box
        */
        private void BuildDataCaption()
        {
            StringBuilder dataStr = new StringBuilder();

            switch (curEndpt.Attributes)
            {
                case 0: dataStr.Append("CONTROL ");
                    break;
                case 1: dataStr.Append("ISOC ");
                    break;
                case 2: dataStr.Append("BULK ");
                    break;
                case 3: dataStr.Append("INTERRUPT ");
                    break;
            }

            if (curEndpt.bIn)
                dataStr.Append("IN transfer ");
            else
                dataStr.Append("OUT transfer ");

            dataCaption = dataStr.ToString();
        }

        /* Summary
            Just to print the values in formatted order in output box
        */
        private void DisplayXferData(byte[] buf, int bCnt, bool TransferStatus)
        {
            StringBuilder dataStr = new StringBuilder();

            string resultStr = "";
            if (bCnt > 0)
            {
                if (TransferStatus)
                {
                    resultStr = dataCaption + "completed\r\n";

                    for (int i = 0; i < bCnt; i++)
                    {
                        if ((i % 16) == 0) dataStr.Append(string.Format("\r\n{0:X4}", i));
                        dataStr.Append(string.Format(" {0:X2}", buf[i]));
                    }
                }
                else
                {
                    resultStr = dataCaption + "failed with Error Code:" + curEndpt.LastError + "\r\n";
                }

                OutputBox.Text += dataStr.ToString() + "\r\n" + resultStr + "\r\n";
            }
            else
            {
                if (TransferStatus)
                {
                    resultStr = "Zero-length data transfer completed\r\n";
                }
                else
                {
                    //if (buf.Length > 0)
                    //{
                    //    for (int i = 0; i < buf.Length; i++)
                    //    {
                    //        if ((i % 16) == 0) dataStr.Append(string.Format("\r\n{0:X4}", i));
                    //        dataStr.Append(string.Format(" {0:X2}", buf[i]));
                    //    }

                    //    resultStr = "\r\nPartial data Transferred\r\n";
                    //}

                    resultStr = dataCaption + "failed with Error Code:" + curEndpt.LastError + "\r\n";

                }

                OutputBox.Text += dataStr.ToString() + "\r\n" + resultStr + "\r\n";
            }



            OutputBox.SelectionStart = OutputBox.Text.Length;
            OutputBox.ScrollToCaret();
        }

        /* Summary
            To resize the Data Transfers TabPage in case Control endpoint is selected
        */
        private void Form1_Resize(object sender, EventArgs e)
        {
            bool bControlEpt = ((curEndpt != null) && (curEndpt.Attributes == 0));

            DirectionBox.Visible = bControlEpt;
            DirectionLabel.Visible = bControlEpt;
            ReqTypeBox.Visible = bControlEpt;
            ReqTypeLabel.Visible = bControlEpt;
            TargetBox.Visible = bControlEpt;
            TargetLabel.Visible = bControlEpt;
            ReqCodeBox.Visible = bControlEpt;
            ReqCodeLabel.Visible = bControlEpt;
            wValueBox.Visible = bControlEpt;
            wValueLabel.Visible = bControlEpt;
            wIndexBox.Visible = bControlEpt;
            wIndexLabel.Visible = bControlEpt;
            button1.Visible = bControlEpt;
            initBut.Visible = bControlEpt;
            Button_Init.Visible = bControlEpt;

            int oBoxAdj = bControlEpt ? 200 : 125;

            OutputBox.SetBounds(0, 0, 5, XferTab.Size.Height - oBoxAdj);

            if (Sync_Form_Resize == 1)
            {
                if (curCyUsbDev != null)
                {
                    if (bControlEpt)
                    {
                        this.XferTab.Enabled = true;
                    }
                    else if (curCyUsbDev.IntfcCount == 1 && curCyUsbDev.ConfigCount == 1)
                    {
                        this.XferTab.Enabled = true;
                    }
                    else
                    {
                        this.XferTab.Enabled = false;
                    }
                }
                else if (curHidDev != null)
                {
                    this.XferTab.Enabled = true;
                }

                Sync_Form_Resize = 0;
            }
        }

        /* Summary
            Event handler for the XferTextBox
        */
        private void XferTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            string txt = XferTextBox.Text;
            StringBuilder hexData = new StringBuilder();

            foreach (char c in txt)
            {
                hexData.Append(string.Format("{0:X2} ", Convert.ToByte(c)));
            }

            XferDataBox.Text = hexData.ToString();
            NumBytesBox.Text = XferTextBox.Text.Length.ToString();

            HidLimitXferLen();
        }

        /* Summary
            Event handler for the XferDataBox
        */
        private void XferDataBox_KeyUp(object sender, KeyEventArgs e)
        {
            string hexData = XferDataBox.Text;
            string[] hexTokens = hexData.Split(' ');

            StringBuilder txt = new StringBuilder();

            int nToks = 0;

            foreach (string tok in hexTokens)
                if (tok.Length > 0)
                {
                    nToks++;

                    try
                    {
                        int n = Convert.ToInt32(tok, 16);
                        txt.Append(Convert.ToChar(n));
                    }
                    catch (Exception exc)
                    {
                        MessageBox.Show(exc.Message, "Input Error");
                        return;
                    }
                }

            XferTextBox.Text = txt.ToString();
            NumBytesBox.Text = nToks.ToString();

            HidLimitXferLen();
        }

        private void HidLimitXferLen()
        {
            TreeNode selNode = DeviceTreeView.SelectedNode;

            if (curHidDev != null) NumBytesBox.Text = curHidReport.RptByteLen.ToString();
        }

        /* Summary
            Handler to close application
        */
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        /* Summary
            Method to check whether Fx2 is selected or not
        */
        private CyFX2Device Fx2DeviceSelected()
        {
            TreeNode selNode = DeviceTreeView.SelectedNode;

            if (selNode == null)
            {
                MessageBox.Show("Select an FX2 device in the device tree.", "Non-FX2 device selected");
                return null;
            }

            // Climb to the top of the tree
            while (selNode.Parent != null)
                selNode = selNode.Parent;

            fx2 = selNode.Tag as CyFX2Device;

            if (fx2 == null)
                MessageBox.Show("Select an FX2 device in the device tree.", "Non-FX2 device selected");

            return fx2;
        }

        /* Summary
            Used for some specific purposes for not showing the MessageBox similar to Fx2DeviceSelected
        */
        private CyFX2Device Fx2DeviceSelected_forusage()
        {
            TreeNode selNode = DeviceTreeView.SelectedNode;

            while (selNode.Parent != null)
                selNode = selNode.Parent;

            fx2 = selNode.Tag as CyFX2Device;

            return fx2;
        }

        /* Summary
            This event handler handles 3 events: programming large EEprom, small EEprom and loading RAM
        */
        private void ProgE2Item_Click(object sender, EventArgs e)
        {
            fx2 = Fx2DeviceSelected();
            string tmpFilter = FOpenDialog.Filter;

            if ((sender == ProgE2Item) || (sender == ProgSE2Item))
                FOpenDialog.Filter = "Firmware Image files (*.iic) | *.iic";

            if ((fx2 != null) && (FOpenDialog.ShowDialog() == DialogResult.OK))
            {
                bool bResult = false;

                if (sender == ProgE2Item)
                {
                    StatLabel.Text = "Programming EEPROM of " + fx2.FriendlyName;
                    Refresh();
                    bResult = fx2.LoadEEPROM(FOpenDialog.FileName, true);
                }
                else if (sender == ProgSE2Item)
                {
                    StatLabel.Text = "Programming EEPROM of " + fx2.FriendlyName;
                    Refresh();
                    bResult = fx2.LoadEEPROM(FOpenDialog.FileName, false);
                }
                else
                {
                    StatLabel.Text = "Programming RAM of " + fx2.FriendlyName;
                    Refresh();
                    bResult = fx2.LoadExternalRam(FOpenDialog.FileName);
                }
                StatLabel.Text = "Programming " + (bResult ? "succeeded." : "failed.");
                Refresh();
            }

            FOpenDialog.FileName = "";
            FOpenDialog.Filter = tmpFilter;
        }

        /* Summary
            Event handler to reset or run Fx2's CPU
        */
        private void HaltItem_Click(object sender, EventArgs e)
        {
            fx2 = Fx2DeviceSelected();

            if (fx2 != null)
                if (sender == HaltItem)
                    fx2.Reset(1);
                else
                    fx2.Reset(0);

        }

        /* Summary
            Event handler to handle changes in Check box of Device Class Selection Tabpage and at Start
        */
        private void CyUSBDeviceBox_CheckedChanged(object sender, EventArgs e)
        {
            byte DeviceMask = 0;

            DeviceMask |= CyUSBDeviceBox.Checked ? CyConst.DEVICES_CYUSB : (byte)0;
            DeviceMask |= MSCDeviceBox.Checked ? CyConst.DEVICES_MSC : (byte)0;
            DeviceMask |= HIDDeviceBox.Checked ? CyConst.DEVICES_HID : (byte)0;

            if (usbDevices != null)
            {
                usbDevices.DeviceRemoved -= usbDevices_DeviceRemoved;
                usbDevices.DeviceAttached -= usbDevices_DeviceAttached;
                usbDevices.Dispose();
            }

            usbDevices = new USBDeviceList(DeviceMask);

            usbDevices.DeviceRemoved += new EventHandler(usbDevices_DeviceRemoved);
            usbDevices.DeviceAttached += new EventHandler(usbDevices_DeviceAttached);

            curEndpt = null;
            curCyUsbDev = null;
            curHidDev = null;
            curHidReport = null;
            RefreshDeviceTree();
        }

        /* Summary
            Event handler for new device attach
        */
        void usbDevices_DeviceAttached(object sender, EventArgs e)
        {
            USBEventArgs usbEvent = e as USBEventArgs;

            RefreshDeviceTree();
        }

        /* Summary
            Event handler for device removal
        */
        void usbDevices_DeviceRemoved(object sender, EventArgs e)
        {
            USBEventArgs usbEvent = e as USBEventArgs;
            curEndpt = null; // reinitialize
            RefreshDeviceTree();
        }

        /* Summary
            Clears the outputbox and status bar
        */
        private void Clear_Click(object sender, EventArgs e)
        {
            OutputBox.Text = "";
            StatLabel.Text = "";
            StatusLabel.Text = "";
        }

        /* Summary
            Aborts the selected pipe
        */
        private void Abort_Pipe_Click(object sender, EventArgs e)
        {
            fx2 = Fx2DeviceSelected();

            if (fx2 == null)
            {
                return;
            }

            if (curEndpt == null)
            {
                MessageBox.Show("Select an endpoint to Abort.", "No endpoint selected");
                return;
            }
            curEndpt.Abort();
            StatLabel.Text = "Abort Pipe Successfull";
        }

        /* Summary
            Resets the selected pipe
        */
        private void Reset_Pipe_Click(object sender, EventArgs e)
        {
            fx2 = Fx2DeviceSelected();

            if (fx2 == null)
            {
                return;
            }

            if (curEndpt == null)
            {
                MessageBox.Show("Select an endpoint to reset.", "No endpoint selected");
                return;
            }
            curEndpt.Reset();
            StatLabel.Text = "Reset Pipe Successfull";
        }

        /* Summary
            Aborts the selected endpoint
        */
        private void Abort_endpoint_Click(object sender, EventArgs e)
        {
            fx2 = Fx2DeviceSelected();

            if (fx2 == null)
            {
                return;
            }

            if (curEndpt == null)
            {
                MessageBox.Show("Select an endpoint to Abort.", "No endpoint selected");
                return;
            }
            curEndpt.Abort();
            StatLabel.Text = "Abort Successfull";
        }

        /* Summary
            Resets the selected endpoint
        */
        private void Reset_endpoint_Click(object sender, EventArgs e)
        {
            fx2 = Fx2DeviceSelected();

            if (fx2 == null)
            {
                return;
            }

            if (curEndpt == null)
            {
                MessageBox.Show("Select an endpoint to reset.", "No endpoint selected");
                return;
            }
            curEndpt.Reset();
            StatLabel.Text = "Reset Successfull";
        }

        /* Summary
            Reconnects the selected Fx2 device
        */
        private void Reconnect_device_Click(object sender, EventArgs e)
        {
            fx2 = Fx2DeviceSelected();

            if (fx2 == null)
            {
                return;
            }
            fx2.ReConnect();
            StatLabel.Text = "Device Reconnected";
        }

        /* Summary
            Resets the selected Fx2 device
        */
        private void Reset_device_Click(object sender, EventArgs e)
        {
            fx2 = Fx2DeviceSelected();

            if (fx2 == null)
            {
                return;
            }
            fx2.Reset();
            RefreshDeviceTree();
            StatLabel.Text = "Device Reset Successfull";

        }

        /* Summary
            Shows the last state and status of endpoint
        */
        private void URB_Stat_Click(object sender, EventArgs e)
        {
            fx2 = Fx2DeviceSelected();

            if (fx2 == null)
            {
                return;
            }

            if (curEndpt == null)
            {
                MessageBox.Show("Select an endpoint to find the status.", "No endpoint selected");
                return;
            }

            uint status = curEndpt.UsbdStatus;

            string Hex = String.Format("{0:X}", status);

            StatLabel.Text = "Last URB Error = 0x" + Hex + " " + CyUSBDevice.UsbdStatusString(status);

        }

        /* Summary
            Generates a .spt file --> asks for source .hex
        */
        private void Create_script_Click(object sender, EventArgs e)
        {
            string tmpFilter = FOpenDialog.Filter;
            string tmpTitle = FOpenDialog.Title;

            FOpenDialog.Title = "Select a hex file: ";
            FOpenDialog.Filter = "Intel HEX files (*.hex) | *.hex";

            if (FOpenDialog.ShowDialog() == DialogResult.OK)
            {
                fname = FOpenDialog.FileName;
                Refresh();
            }
            else
            {
                FOpenDialog.Title = tmpTitle;
                FOpenDialog.Filter = tmpFilter;
                return;
            }

            FOpenDialog.FileName = "";
            FOpenDialog.Title = tmpTitle;
            FOpenDialog.Filter = tmpFilter;

            string tmpSFilter = FSave.Filter;
            string tmpSTitle = FSave.Title;

            FSave.Filter = "Save script file (*.spt) | *.spt";


            if (FSave.ShowDialog() == DialogResult.OK)
            {
                scriptfile = FSave.FileName;
                Refresh();
            }
            else
            {
                FSave.Filter = tmpSFilter;
                FSave.Title = tmpSTitle;
                return;
            }

            FSave.FileName = "";
            FSave.Filter = tmpSFilter;
            FSave.Title = tmpSTitle;

            try
            {
                stream = new FileStream(scriptfile, FileMode.Create);
                sw = new StreamWriter(stream);

                LoadHexScript("VendAX.hex", true);
                LoadHexScript(fname, false);
                LoadHexScript(fname, true);

                sw.Close();
                stream.Close();
                scriptfile = "";

                StatLabel.Text = "Script Created: " + FSave.FileName;
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message, "Check the File Attributes");
            }
        }

        /* Summary
            Used by Create_Script
        */
        public void LoadHexScript(string fname, bool blow)
        {
            list.Clear();
            list1.Clear();

            string line, sOffset, tmp;
            int v;

            if (fname.Equals("VendAX.hex"))
            {
                //Fills the vendax firmware into the Arraylist list
                AddVend_to_list();
            }
            else
            {
                //Fills the list from hex file
                FileStream fs = new FileStream(fname, FileMode.Open, FileAccess.Read);
                StreamReader sr = new StreamReader(fs);
                while (!sr.EndOfStream)
                {
                    list.Add(sr.ReadLine());
                }
                sr.Close();
                fs.Close();
            }

            int Ramsize = Maxaddr;
            string AddrBoundary = textBox2.Text.Substring(2, 4);

            // Delete non-data records
            for (int i = list.Count - 1; i >= 0; i--)
            {
                line = (string)list[i];
                if (line.Length > 0)
                {
                    tmp = line.Substring(7, 2);   // Get the Record Type into v
                    v = (int)Util.HexToInt(tmp);
                    if (v != 0) list.Remove(list[i]);   // Data records are type == 0
                }
            }

            for (int i = 0; i < list.Count; i++)
            {
                line = (string)list[i];

                // Remove comments
                v = line.IndexOf("//");
                if (v > -1)
                    line = line.Substring(0, v - 1);

                // Build string that just contains the offset followed by the data bytes
                if (line.Length > 0)
                {
                    // Get the offset
                    sOffset = line.Substring(3, 4);

                    // Get the string of data chars
                    tmp = line.Substring(1, 2);
                    v = (int)Util.HexToInt(tmp) * 2;
                    string s = line.Substring(9, v);

                    list1.Add(sOffset + s);
                }

            }

            if (blow) Hold(true);

            Reqcode = blow ? (byte)0xA0 : (byte)0xA3;
            windex = 0;

            int iindex = 0;
            Datastring = "";
            int nxtoffset = 0;
            int xferLen = 0;
            wvalue = 0;

            foreach (string lines in list1)
            {
                line = lines.Substring(0, 4);
                ushort offset = (ushort)Util.HexToInt(line);

                int slen = lines.Length;

                int no_bytes = (slen - 4) / 2;
                int lastaddr = offset + no_bytes;

                if ((blow && (lastaddr < Ramsize)) || (!blow && (lastaddr >= Ramsize)))
                {
                    xferLen += no_bytes;

                    if ((offset == nxtoffset) && (xferLen < Max_Ctlxfer_size))
                    {
                        Datastring += lines.Substring(4, no_bytes * 2);
                    }
                    else
                    {
                        int len = Datastring.Length;


                        if (!len.Equals(0))
                        {
                            int bufLen = len / 2;
                            byte[] buf = new byte[bufLen];
                            string d;

                            for (int j = 0; j < bufLen; j++)
                            {
                                d = Datastring.Substring(j * 2, 2);
                                buf[j] = (byte)Util.HexToInt(d);
                            }

                            Write_script(ref buf, ref bufLen);

                        }

                        wvalue = (ushort)Util.HexToInt(line);
                        Datastring = lines.Substring(4, no_bytes * 2);
                        xferLen = no_bytes;
                    }

                    nxtoffset = offset + no_bytes;
                }
                iindex++;
            }

            int len1 = Datastring.Length;
            if (!len1.Equals(0))
            {
                int bufLen = len1 / 2;
                byte[] buf1 = new byte[bufLen];
                string d;

                for (int j = 0; j < bufLen; j++)
                {
                    d = Datastring.Substring(j * 2, 2);
                    buf1[j] = (byte)Util.HexToInt(d);
                }

                Write_script(ref buf1, ref bufLen);

            }

            if (blow) Hold(false);
        }

        /* Summary
            For resetting the CPU or reverse
        */
        public void Hold(bool bHold)
        {
            byte[] buf = new byte[1];

            buf[0] = (bHold) ? (byte)1 : (byte)0;
            int bufLen = 1;

            Reqcode = 0xA0;
            wvalue = Resetreg;
            windex = 0;

            Write_script(ref buf, ref bufLen);
        }

        /* Summary
            Final step of Create_Script --> writes to a opened stream
        */
        public void Write_script(ref byte[] buffer, ref int buflen)
        {
            int len = buflen;

            Xaction.bReqType = CyConst.DIR_TO_DEVICE | CyConst.REQ_VENDOR | CyConst.TGT_DEVICE;
            Xaction.CtlReqCode = Reqcode;
            Xaction.wValue = wvalue;
            Xaction.wIndex = windex;

            Xaction.DataLen = (uint)len;
            Xaction.Timeout = 15;
            Xaction.RecordSize = (uint)len + TTransaction.TotalHeaderSize;

            Xaction.WriteToStream(stream);
            Xaction.WriteFromBuffer(stream, ref buffer, ref buflen);
        }

        /* Summary
            Get's the monitor's filename to load
        */
        private void Select_Monitor_Click(object sender, EventArgs e)
        {
            fx2 = Fx2DeviceSelected();

            if (fx2 == null)
            {
                return;
            }

            string tmpFilter = FOpenDialog.Filter;
            string tmpTitle = FOpenDialog.Title;

            FOpenDialog.Title = "Select a Monitor for Debugging: ";
            FOpenDialog.Filter = "Intel HEX files (*.hex) | *.hex";

            if (FOpenDialog.ShowDialog() == DialogResult.OK)
            {
                monitorfile = FOpenDialog.FileName;
                StatLabel.Text = "Monitor Selected.... " + FOpenDialog.FileName;
                Refresh();
            }
            else
            {
                FOpenDialog.Title = tmpTitle;
                FOpenDialog.Filter = tmpFilter;
                return;
            }

            FOpenDialog.FileName = "";
            FOpenDialog.Title = tmpTitle;
            FOpenDialog.Filter = tmpFilter;

            Load_Monitor.Enabled = true;
        }

        /* Summary
            Executes the selected monitor
        */
        private void Load_Monitor_Click(object sender, EventArgs e)
        {
            fx2 = Fx2DeviceSelected();

            if (fx2 == null)
            {
                return;
            }

            if (monitorfile.Length == 0)
            {
                MessageBox.Show("Select a monitor before loading.", "Select Monitor");
                return;
            }

            fx2.LoadExternalRam(monitorfile);

            monitorfile = "";
            Load_Monitor.Enabled = false;
        }

        /* Summary
            Just get's the file name of .spt for playing
        */
        private void load_button_Click(object sender, EventArgs e)
        {
            fx2 = Fx2DeviceSelected();

            if (fx2 == null)
            {
                return;
            }

            string tmpFilter = FOpenDialog.Filter;
            string tmpTitle = FOpenDialog.Title;

            FOpenDialog.Title = "Select a Script file to load: ";
            FOpenDialog.Filter = "Script files (*.spt) | *.spt";

            if (FOpenDialog.ShowDialog() == DialogResult.OK)
            {
                playscriptfile = FOpenDialog.FileName;
                StatLabel.Text = "Script loaded.... " + FOpenDialog.FileName;
                Refresh();
            }
            else
            {
                FOpenDialog.Title = tmpTitle;
                FOpenDialog.Filter = tmpFilter;
                return;
            }

            FOpenDialog.FileName = "";
            FOpenDialog.Title = tmpTitle;
            FOpenDialog.Filter = tmpFilter;

            play_button.Enabled = true;
        }

        /* Summary
            Executes the script loaded
        */
        private void play_button_Click(object sender, EventArgs e)
        {
            fx2 = Fx2DeviceSelected();

            if (fx2 == null)
            {
                return;
            }

            if (playscriptfile.Length == 0)
            {
                MessageBox.Show("Load a script before playing it.", "Load script");
                return;
            }

            StatLabel.Text = "Playing Script " + FOpenDialog.FileName + " in Outputbox";
            Refresh();

            FileStream stream = new FileStream(playscriptfile, FileMode.Open, FileAccess.Read);

            if (stream.Length > 0)
            {
                try
                {
                    Xaction.ReadFromStream(stream);

                    if (fx2.Config != Xaction.ConfigNum)
                        fx2.Config = Xaction.ConfigNum;

                    if (fx2.AltIntfc != Xaction.AltIntfc)
                        fx2.AltIntfc = Xaction.AltIntfc;

                    stream.Close();

                    stream = new FileStream(playscriptfile, FileMode.Open, FileAccess.Read);

                    long totalFileSize = stream.Length;
                    long file_bytes_read = 0;

                    do
                    {
                        Xaction.ReadFromStream(stream);
                        file_bytes_read += 32;

                        if (Xaction.Tag == 0xFF)
                        {
                            Thread.Sleep(100);
                        }
                        else
                        {
                            byte[] buffer = new byte[Xaction.DataLen];
                            int len = (int)Xaction.DataLen;

                            curEndpt = fx2.EndPointOf(Xaction.EndPtAddr);

                            if (curEndpt != null)
                            {
                                if (curEndpt.Attributes == 0)
                                {
                                    /* Control transfer */
                                    CyControlEndPoint ctlEpt = curEndpt as CyControlEndPoint;

                                    byte tmp = Xaction.bReqType;

                                    ctlEpt.Target = (byte)(tmp & TTransaction.ReqType_TGT_MASK);
                                    ctlEpt.ReqType = (byte)(tmp & TTransaction.ReqType_TYPE_MASK);
                                    ctlEpt.Direction = (byte)(tmp & TTransaction.ReqType_DIR_MASK);
                                    ctlEpt.ReqCode = Xaction.CtlReqCode;
                                    ctlEpt.Value = Xaction.wValue;
                                    ctlEpt.Index = Xaction.wIndex;

                                    if (Xaction.Tag == 0)
                                    {
                                        Xaction.ReadToBuffer(stream, ref buffer, ref len);
                                        file_bytes_read += len;
                                    }

                                    if (Xaction.Tag == 1)
                                    {
                                        /* Read from device saving to file */

                                        string tmpSFilter = FSave.Filter;
                                        string tmpSTitle = FSave.Title;
                                        string file;

                                        FSave.Title = "Save Data as:";
                                        FSave.Filter = "All Files(*.*) | *.*";


                                        if (FSave.ShowDialog() == DialogResult.OK)
                                        {
                                            file = FSave.FileName;
                                            Refresh();
                                        }
                                        else
                                        {
                                            FSave.Filter = tmpSFilter;
                                            FSave.Title = tmpSTitle;
                                            return;
                                        }

                                        FSave.FileName = "";
                                        FSave.Filter = tmpSFilter;
                                        FSave.Title = tmpSTitle;

                                        PerformCtlFileTransfer(file, ref buffer, ref len);
                                    }
                                    else
                                    {
                                        PerformCtlTransfer(ref buffer, ref len);
                                    }
                                }
                                else
                                {
                                    /* Non Ep0 transfer */
                                    if (Xaction.Tag == 0)
                                    {
                                        Xaction.ReadToBuffer(stream, ref buffer, ref len);
                                        file_bytes_read += len;
                                    }

                                    if (Xaction.Tag == 1)
                                    {
                                        /* Read from device saving to file */
                                        string tmpSFilter = FSave.Filter;
                                        string tmpSTitle = FSave.Title;
                                        string file;

                                        FSave.Title = "Save Data as:";
                                        FSave.Filter = "All files(*.*) | *.*";


                                        if (FSave.ShowDialog() == DialogResult.OK)
                                        {
                                            file = FSave.FileName;
                                            Refresh();
                                        }
                                        else
                                        {
                                            FSave.Filter = tmpSFilter;
                                            FSave.Title = tmpSTitle;
                                            return;
                                        }

                                        FSave.FileName = "";
                                        FSave.Filter = tmpSFilter;
                                        FSave.Title = tmpSTitle;

                                        PerformNonEP0FileXfer(file, ref buffer, ref len);
                                    }
                                    else
                                    {
                                        PerformNonEP0Xfer(ref buffer, ref len);
                                    }
                                }
                            }
                        }
                    } while ((totalFileSize - file_bytes_read) >= 32);
                }
                catch (Exception esc)
                {
                    MessageBox.Show(esc.Message, "Invalid file data");
                }
            }
            else
                MessageBox.Show("Script Loaded is empty", "Invalid file");

            stream.Close();            
        }

        /* Summary
            Used for control transfer
        */
        public void PerformCtlTransfer(ref byte[] buffer, ref int buflen)
        {
            bool success;
            CyControlEndPoint ctlEpt = curEndpt as CyControlEndPoint;
            ctlEpt.TimeOut = 5000;
            if (ctlEpt == null)
                return;

            if (ctlEpt.Direction.Equals(CyConst.DIR_FROM_DEVICE))
            {
                StatLabel.Text = "Device --> Host";
                success = ctlEpt.Read(ref buffer, ref buflen);
                if (success)
                {
                    BuildDataCaption();
                    DisplayXferData(buffer, buflen, true);

                    OutputBox.SelectionStart = OutputBox.Text.Length;
                    OutputBox.ScrollToCaret();

                    //Just to print in formatted order in output box can even use DisplayXferData(buffer, buflen);

                    /*string buf = "";
                    int k = 0x0000;
                    for (int x = 0; x < buflen; x++)
                        buf += String.Format("{0:X2}", buffer[x]) + " ";

                    if (buflen > 16)
                    {
                        string[] temp = buf.Split(' ');
                        string print = "";
                        int s = 0;

                        while (s < temp.Length)
                        {
                            if (s != 0)
                            {
                                if (s % 16 == 0)
                                {
                                    OutputBox.Text += String.Format("{0:X4}", k) + " : " + print + "\r\n";
                                    OutputBox.SelectionStart = OutputBox.Text.Length;
                                    OutputBox.ScrollToCaret();
                                    print = "";
                                    print += temp[s] + " ";
                                    k = k + 0x0010;
                                }
                                else
                                {
                                    print += temp[s] + " ";
                                }
                                s++;
                            }
                            else
                            {
                                print += temp[s] + " ";
                                s++;
                            }

                        }
                        if (buflen % 16 != 0)
                        {
                            OutputBox.Text += String.Format("{0:X4}", k) + " : " + print + "\r\n";
                            OutputBox.SelectionStart = OutputBox.Text.Length;
                            OutputBox.ScrollToCaret();
                        }

                    }
                    else
                    {
                        OutputBox.Text += "0000 : " + buf + "\r\n";
                        OutputBox.SelectionStart = OutputBox.Text.Length;
                        OutputBox.ScrollToCaret();
                    }*/
                    //End print in formatted order
                }
                else
                {
                    OutputBox.Text += "Vendor request failed with Error Code:"+curEndpt.LastError+"\r\n";
                }
            }
            else
            {
                StatLabel.Text = "Host --> Device";
                success = ctlEpt.Write(ref buffer, ref buflen);
                if (success)
                {
                    if (ctlEpt.ReqCode == 0xA0)
                        OutputBox.Text += "Download " + buflen + " bytes: addr=" + String.Format("{0:X4}", ctlEpt.Value);
                    else
                        OutputBox.Text += "Vendor Request 0x" + String.Format("{0:X2}", ctlEpt.ReqCode) + " " + buflen + " bytes: wValue=" + String.Format("{0:X4}", ctlEpt.Value) + " wIndex=" + ctlEpt.Index;


                    BuildDataCaption();
                    DisplayXferData(buffer, buflen, true);

                    OutputBox.SelectionStart = OutputBox.Text.Length;
                    OutputBox.ScrollToCaret();

                    //Just to print in formatted order in output box can even use DisplayXferData(buffer, buflen);

                    /*string buf = "";
                    int k = 0x0000;
                    for (int x = 0; x < buflen; x++)
                        buf += String.Format("{0:X2}", buffer[x]) + " ";


                    if (buflen > 16)
                    {
                        string[] temp = buf.Split(' ');
                        string print = "";
                        int s = 0;

                        while (s < temp.Length)
                        {
                            if (s != 0)
                            {
                                if (s % 16 == 0)
                                {
                                    OutputBox.Text += String.Format("{0:X4}", k) + " : " + print + "\r\n";
                                    OutputBox.SelectionStart = OutputBox.Text.Length;
                                    OutputBox.ScrollToCaret();
                                    print = "";
                                    print += temp[s] + " ";
                                    k = k + 0x0010;
                                }
                                else
                                {
                                    print += temp[s] + " ";
                                }
                                s++;
                            }
                            else
                            {
                                print += temp[s] + " ";
                                s++;
                            }

                        }
                        if (buflen % 16 != 0)
                        {
                            OutputBox.Text += String.Format("{0:X4}", k) + " : " + print + "\r\n";
                            OutputBox.SelectionStart = OutputBox.Text.Length;
                            OutputBox.ScrollToCaret();
                        }
                    }
                    else
                    {
                        OutputBox.Text += "0000 : " + buf + "\r\n";
                        OutputBox.SelectionStart = OutputBox.Text.Length;
                        OutputBox.ScrollToCaret();
                    }*/
                    //End print in formatted order

                }
                else
                {
                    OutputBox.Text += "Vendor request failed with Error Code:" + curEndpt.LastError + "\r\n";
                }
            }
        }

        /*
            Used to perform control file transfer
        */
        public void PerformCtlFileTransfer(string fname, ref byte[] buf, ref int len)
        {
            bool success;

            CyControlEndPoint ctlEpt = curEndpt as CyControlEndPoint;

            if (ctlEpt == null)
                return;

            if (ctlEpt.bIn)
            {
                ctlEpt.TimeOut = 5000;
                success = ctlEpt.Read(ref buf, ref len);

                if (success)
                {
                    try
                    {
                        FileStream file = new FileStream(fname, FileMode.Create);
                        file.Write(buf, 0, len);
                        file.Close();
                    }
                    catch (Exception exc)
                    {
                        MessageBox.Show(exc.Message, "Check the File Attributes");
                    }

                    OutputBox.Text += "Control Transfer complete: " + len + " Bytes read";
                    BuildDataCaption();
                    DisplayXferData(buf, len, true);

                    OutputBox.SelectionStart = OutputBox.Text.Length;
                    OutputBox.ScrollToCaret();
                }
                else
                {
                    OutputBox.Text += "Control Transfer failed with Error Code:"+curEndpt.LastError+"\r\n";
                    OutputBox.SelectionStart = OutputBox.Text.Length;
                    OutputBox.ScrollToCaret();
                }

            }
            else
            {
                FileStream file = new FileStream(fname, FileMode.Open, FileAccess.Read);
                file.Read(buf, 0, len);
                file.Close();

                ctlEpt.TimeOut = 25000;
                success = ctlEpt.Write(ref buf, ref len);

                if (success)
                {
                    BuildDataCaption();
                    DisplayXferData(buf, len, true);
                    OutputBox.Text += "File transfer successful. " + len + " Bytes transferred\r\n";

                    OutputBox.SelectionStart = OutputBox.Text.Length;
                    OutputBox.ScrollToCaret();
                }
                else
                {
                    OutputBox.Text += "Control Transfer failed with Error Code:" + curEndpt.LastError + "\r\n";
                    OutputBox.SelectionStart = OutputBox.Text.Length;
                    OutputBox.ScrollToCaret();
                }
            }
        }

        /* Summary
            Used for transfers other than control
        */
        public void PerformNonEP0Xfer(ref byte[] buffer, ref int buflen)
        {
            bool success;
            string stat = "";

            if (curEndpt == null)
                return;

            switch (curEndpt.Attributes)
            {
                case 1: stat = "Isoc Transfer";
                    break;
                case 2: stat = "Bulk Transfer";
                    break;
                case 3: stat = "Interrupt Transfer";
                    break;
            }

            curEndpt.TimeOut = 5000;
            success = curEndpt.XferData(ref buffer, ref buflen);



            if (success)
            {
                if (curEndpt.bIn)
                {
                    OutputBox.Text += stat + " Read Success";
                    BuildDataCaption();
                    DisplayXferData(buffer, buflen, true);

                    OutputBox.SelectionStart = OutputBox.Text.Length;
                    OutputBox.ScrollToCaret();
                }
                else
                {
                    OutputBox.Text += stat + " Write Success";
                    BuildDataCaption();
                    DisplayXferData(buffer, buflen, true);

                    OutputBox.SelectionStart = OutputBox.Text.Length;
                    OutputBox.ScrollToCaret();
                }
            }
            else
            {
                OutputBox.Text += stat + " Transfer failed with Error Code:" + curEndpt.LastError + "\r\n";
                OutputBox.SelectionStart = OutputBox.Text.Length;
                OutputBox.ScrollToCaret();
            }
        }

        /* Summary
            Used for file transfers in BULK/Inter/ISO
        */
        public void PerformNonEP0FileXfer(string fname, ref byte[] buf, ref int len)
        {
            bool success;
            string stat = "";

            if (curEndpt == null)
                return;

            switch (curEndpt.Attributes)
            {
                case 1: stat = "Isoc Transfer";
                    break;
                case 2: stat = "Bulk Transfer";
                    break;
                case 3: stat = "Interrupt Transfer";
                    break;
            }

            if (curEndpt.bIn)
            {
                curEndpt.TimeOut = 5000;
                success = curEndpt.XferData(ref buf, ref len);

                if (success)
                {
                    try
                    {
                        FileStream file = new FileStream(fname, FileMode.Create);
                        file.Write(buf, 0, len);
                        file.Close();
                    }
                    catch (Exception exc)
                    {
                        MessageBox.Show(exc.Message, "Check the File Attributes");
                    }

                    OutputBox.Text += stat + " Read Success";
                    BuildDataCaption();
                    DisplayXferData(buf, len, true);

                    OutputBox.SelectionStart = OutputBox.Text.Length;
                    OutputBox.ScrollToCaret();
                }
                else
                {
                    OutputBox.Text += stat + " Transfer failed with Error Code:" + curEndpt.LastError + "\r\n";
                    OutputBox.SelectionStart = OutputBox.Text.Length;
                    OutputBox.ScrollToCaret();
                }

            }
            else
            {
                FileStream file = new FileStream(fname, FileMode.Open, FileAccess.Read);
                file.Read(buf, 0, len);
                file.Close();

                curEndpt.TimeOut = 25000;
                success = curEndpt.XferData(ref buf, ref len);

                if (success)
                {
                    OutputBox.Text += stat + " Write Success";
                    BuildDataCaption();
                    DisplayXferData(buf, len, true);

                    OutputBox.SelectionStart = OutputBox.Text.Length;
                    OutputBox.ScrollToCaret();
                }
                else
                {
                    OutputBox.Text += stat + " Transfer failed with Error Code:" + curEndpt.LastError + "\r\n";
                    OutputBox.SelectionStart = OutputBox.Text.Length;
                    OutputBox.ScrollToCaret();
                }
            }
        }

        /* Summary
            To get the scripting parameters
        */
        private void Ok_Click(object sender, EventArgs e)
        {
            gb.Visible = false;

            //Resetreg = (ushort)Util.HexToInt(textBox1.Text);
            //Maxaddr = (ushort)Util.HexToInt(textBox2.Text);

            try
            {
                Resetreg = (ushort)Convert.ToInt16(textBox1.Text, 16);
                Maxaddr = (ushort)Convert.ToInt16(textBox2.Text, 16);
            }
            catch(Exception exc)
            {
                MessageBox.Show(exc.Message + " Using default values","Input Error");
                Resetreg = (ushort)Convert.ToInt16("0xE600", 16);
                Maxaddr = (ushort)Convert.ToInt16("0x4000", 16);
            }
        }

        private void Script_parameters_Click(object sender, EventArgs e)
        {
            gb.Visible = true;
        }

        private void UsersGuide_Click(object sender, EventArgs e)
        {
            try
            {
                //This was done relative to installation directory
                try
                {
                    System.Diagnostics.Process.Start("..\\..\\..\\..\\CyControlCenter.chm");
                }
                catch
                {
                    System.Diagnostics.Process.Start("..\\CyControlCenter.chm");
                }
            }
            catch
            {
                //Original path
                try
                {
                    System.Diagnostics.Process.Start("C:\\Program Files\\Cypress Semiconductor\\Cypress Suite USB\\CyUSB.NET\\CyControlCenter.chm");
                }
                catch
                {
                    //For Shortcut
                    try
                    {
                        string path = Application.StartupPath;

                        string[] Tokens = path.Split('\\');
                        string create_path = "";

                        for (int i = 0; i < Tokens.Length - 1; i++)
                        {
                            create_path += Tokens[i].ToString();
                            create_path += "\\\\";
                        }

                        System.Diagnostics.Process.Start(create_path + "CyControlCenter.chm");
                    }
                    catch
                    {
                        MessageBox.Show("Can't find the file", "CyControlCenter.chm");
                    }
                }
            }
        }


        private string GetOSName()
        {
            OperatingSystem osInfo = Environment.OSVersion;
            string osName = "UNKNOWN";


            switch (osInfo.Platform)
            {
                case PlatformID.Win32Windows:
                    {
                        switch (osInfo.Version.Minor)
                        {
                            case 0:
                                {
                                    osName = "Windows 95";
                                    break;
                                }

                            case 10:
                                {
                                    if (osInfo.Version.Revision.ToString() == "2222A")
                                    {
                                        osName = "Windows 98 Second Edition";
                                    }
                                    else
                                    {
                                        osName = "Windows 98";
                                    }
                                    break;
                                }

                            case 90:
                                {
                                    osName = "Windows Me";
                                    break;
                                }
                        }
                        break;
                    }

                case PlatformID.Win32NT:
                    {
                        switch (osInfo.Version.Major)
                        {
                            case 3:
                                {
                                    osName = "Windows NT 3.51";
                                    break;
                                }

                            case 4:
                                {
                                    osName = "Windows NT 4.0";
                                    break;
                                }

                            case 5:
                                {
                                    if (osInfo.Version.Minor == 0)
                                    {
                                        osName = "Windows 2000";
                                    }
                                    else
                                    {
                                        osName = "Windows XP";
                                    }
                                    break;
                                }

                            case 6:
                                {
                                    osName = "Windows Vista";
                                    break;
                                }
                        }
                        break;
                    }
            }

            return osName;
        }

        private void Record_Click(object sender, EventArgs e)
        {
            StatLabel.Text = "Recording.... ";
            bRecording = true;

            try
            {
                script_stream = new FileStream(Application.StartupPath + "\\tmp.spt", FileMode.Create);
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message, "Check the File Attributes");
            }

            Record.Enabled = false;
            Stop.Enabled = true;

        }

        private void Pause_Click(object sender, EventArgs e)
        {
            if (bRecording && (script_stream != null))
            {
                Xaction.ConfigNum = fx2.Config;
                Xaction.IntfcNum = 0;
                Xaction.AltIntfc = fx2.AltIntfc;
                Xaction.Tag = 0xFF;
                Xaction.DataLen = 0;
                Xaction.RecordSize = TTransaction.TotalHeaderSize;

                //Write xaction
                Xaction.WriteToStream(script_stream);

            }
        }

        private void Stop_Click(object sender, EventArgs e)
        {
            bRecording = false;
            StatLabel.Text = "";
            Record.Enabled = true;
            Stop.Enabled = false;

            if (script_stream != null)
            {
                script_stream.Close();
                script_stream = null;

                string tmpSFilter = FSave.Filter;
                string tmpSTitle = FSave.Title;

                FSave.Filter = "Save script file (*.spt) | *.spt";


                if (FSave.ShowDialog() == DialogResult.OK)
                {
                    scriptfile = FSave.FileName;
                    Refresh();
                }
                else
                {
                    FSave.Filter = tmpSFilter;
                    FSave.Title = tmpSTitle;
                    return;
                }

                FSave.FileName = "";
                FSave.Filter = tmpSFilter;
                FSave.Title = tmpSTitle;

                try
                {
                    File.Copy(Application.StartupPath + "\\tmp.spt", scriptfile, true);
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message, "Check the File Attributes");
                }

            }
        }

        /* Summary
            Fills the vendax firmware into the Arraylist list
        */
        public void AddVend_to_list()
        {
            list.Add(":0A0D3E00000102020303040405058E");
            list.Add(":10064D00E4F52CF52BF52AF529C203C200C202C22E");
            list.Add(":10065D0001120C6C7E0A7F008E0A8F0B75120A75C3");
            list.Add(":10066D00131275080A75091C75100A75114A75144F");
            list.Add(":10067D000A751578EE54C07003020752752D00757A");
            list.Add(":10068D002E808E2F8F30C3749A9FFF740A9ECF24B5");
            list.Add(":10069D0002CF3400FEE48F288E27F526F525F524AC");
            list.Add(":1006AD00F523F522F521AF28AE27AD26AC25AB24D9");
            list.Add(":1006BD00AA23A922A821C3120D035037E530252402");
            list.Add(":1006CD00F582E52F3523F583E0FFE52E2524F58210");
            list.Add(":1006DD00E52D3523F583EFF0E4FAF9F8E52424014F");
            list.Add(":1006ED00F524EA3523F523E93522F522E83521F500");
            list.Add(":1006FD002180B3852D0A852E0B74002480FF740A8A");
            list.Add(":10070D0034FFFEC3E5139FF513E5129EF512C3E505");
            list.Add(":10071D000D9FF50DE50C9EF50CC3E50F9FF50FE54F");
            list.Add(":10072D000E9EF50EC3E5099FF509E5089EF508C374");
            list.Add(":10073D00E5119FF511E5109EF510C3E5159FF51513");
            list.Add(":10074D00E5149EF514D2E843D82090E668E04409FC");
            list.Add(":10075D00F090E65CE0443DF0D2AF90E680E054F7D7");
            list.Add(":10076D00F0538EF8C2031207FF30010512039AC22F");
            list.Add(":10077D00013003F2120D6450EDC203120C0D200076");
            list.Add(":10078D001690E682E030E704E020E1EF90E682E0AB");
            list.Add(":0F079D0030E604E020E0E4120BB6120D6680C7D0");
            list.Add(":0107AC00222A");
            list.Add(":0B0D330090E50DE030E402C322D32263");
            list.Add(":10039A0090E6B9E0700302048514700302052E2466");
            list.Add(":1003AA00FE70030205C424FB700302047F14700369");
            list.Add(":1003BA0002047914700302046D1470030204732496");
            list.Add(":1003CA00056003020639120D68400302064590E6ED");
            list.Add(":1003DA00BBE024FE603B14605624FD6016146040A6");
            list.Add(":1003EA0024067075E50A90E6B3F0E50B90E6B4F0E2");
            list.Add(":1003FA00020645120D33500FE51290E6B3F0E513ED");
            list.Add(":10040A0090E6B4F002064590E6A0E04401F0020648");
            list.Add(":10041A0045E50C90E6B3F0E50D90E6B4F00206452A");
            list.Add(":10042A00E50E90E6B3F0E50F90E6B4F002064590CB");
            list.Add(":10043A00E6BAE0FF120BE2AA06A9077B01EA494BDA");
            list.Add(":10044A00600DEE90E6B3F0EF90E6B4F00206459048");
            list.Add(":10045A00E6A0E04401F002064590E6A0E04401F07F");
            list.Add(":10046A00020645120CF1020645120D50020645120B");
            list.Add(":10047A000D48020645120CDF020645120D6A4003BA");
            list.Add(":10048A0002064590E6B8E0247F602B14603C240203");
            list.Add(":10049A006003020524A200E433FF25E0FFA202E480");
            list.Add(":1004AA00334F90E740F0E4A3F090E68AF090E68BB1");
            list.Add(":1004BA007402F0020645E490E740F0A3F090E68A61");
            list.Add(":1004CA00F090E68B7402F002064590E6BCE0547E9A");
            list.Add(":1004DA00FF7E00E0D3948040067C007D0180047C8E");
            list.Add(":1004EA00007D00EC4EFEED4F243EF582740D3EF584");
            list.Add(":1004FA0083E493FF3395E0FEEF24A1FFEE34E68F09");
            list.Add(":10050A0082F583E0540190E740F0E4A3F090E68A94");
            list.Add(":10051A00F090E68B7402F002064590E6A0E04401F2");
            list.Add(":10052A00F0020645120D6C400302064590E6B8E05B");
            list.Add(":10053A0024FE601D2402600302064590E6BAE0B478");
            list.Add(":10054A000105C20002064590E6A0E04401F0020659");
            list.Add(":10055A004590E6BAE0705990E6BCE0547EFF7E0012");
            list.Add(":10056A00E0D3948040067C007D0180047C007D00FD");
            list.Add(":10057A00EC4EFEED4F243EF582740D3EF583E49376");
            list.Add(":10058A00FF3395E0FEEF24A1FFEE34E68F82F58378");
            list.Add(":10059A00E054FEF090E6BCE05480FF131313541F9E");
            list.Add(":1005AA00FFE0540F2F90E683F0E04420F002064566");
            list.Add(":1005BA0090E6A0E04401F0020645120D6E507C90D0");
            list.Add(":1005CA00E6B8E024FE60202402705B90E6BAE0B44C");
            list.Add(":1005DA000104D200806590E6BAE06402605D90E6AC");
            list.Add(":1005EA00A0E04401F0805490E6BCE0547EFF7E0017");
            list.Add(":1005FA00E0D3948040067C007D0180047C007D006D");
            list.Add(":10060A00EC4EFEED4F243EF582740D3EF583E493E5");
            list.Add(":10061A00FF3395E0FEEF24A1FFEE34E68F82F583E7");
            list.Add(":10062A00E04401F0801590E6A0E04401F0800C124D");
            list.Add(":10063A000080500790E6A0E04401F090E6A0E04474");
            list.Add(":02064A0080F03E");
            list.Add(":01064C00228B");
            list.Add(":03003300020D605B");
            list.Add(":040D600053D8EF3243");
            list.Add(":100C6C00D200E4F51A90E678E05410FFC4540F4417");
            list.Add(":090C7C0050F51713E433F51922B9");
            list.Add(":0107FF0022D7");
            list.Add(":020D6400D32298");
            list.Add(":020D6600D32296");
            list.Add(":020D6800D32294");
            list.Add(":080D480090E6BAE0F518D32291");
            list.Add(":100CDF0090E740E518F0E490E68AF090E68B04F098");
            list.Add(":020CEF00D3220E");
            list.Add(":080D500090E6BAE0F516D3228B");
            list.Add(":100CF10090E740E516F0E490E68AF090E68B04F088");
            list.Add(":020D0100D322FB");
            list.Add(":020D6A00D32292");
            list.Add(":020D6C00D32290");
            list.Add(":020D6E00D3228E");
            list.Add(":1000800090E6B9E0245EB40B0040030203989000B0");
            list.Add(":100090009C75F003A4C58325F0C583730201920209");
            list.Add(":1000A000019202010D0200BD0200D70200F302011D");
            list.Add(":1000B0003C02018C02011602012902016290E74014");
            list.Add(":1000C000E519F0E490E68AF090E68B04F090E6A063");
            list.Add(":1000D000E04480F002039890E60AE090E740F0E404");
            list.Add(":1000E00090E68AF090E68B04F090E6A0E04480F081");
            list.Add(":1000F00002039890E740740FF0E490E68AF090E6EF");
            list.Add(":100100008B04F090E6A0E04480F002039890E6BAF9");
            list.Add(":10011000E0F51702039890E67AE054FEF0E490E6EA");
            list.Add(":100120008AF090E68BF002039890E67AE04401F0C2");
            list.Add(":10013000E490E68AF090E68BF002039890E7407432");
            list.Add(":1001400007F0E490E68AF090E68B04F090E6A0E0F9");
            list.Add(":100150004480F07FE87E031207ADD204120B8702C1");
            list.Add(":10016000039890E6B5E054FEF090E6BFE090E68A92");
            list.Add(":10017000F090E6BEE090E68BF090E6BBE090E6B350");
            list.Add(":10018000F090E6BAE090E6B4F002039875190143E6");
            list.Add(":10019000170190E6BAE0753100F532A3E0FEE4EE17");
            list.Add(":1001A000423190E6BEE0753300F534A3E0FEE4EEA4");
            list.Add(":1001B000423390E6B8E064C06003020282E5344551");
            list.Add(":1001C00033700302039890E6A0E020E1F9C3E53420");
            list.Add(":1001D0009440E533940050088533358534368006E5");
            list.Add(":1001E00075350075364090E6B9E0B4A335E4F537CF");
            list.Add(":1001F000F538C3E5389536E53795355060E5322555");
            list.Add(":1002000038F582E5313537F583E0FF74402538F560");
            list.Add(":1002100082E434E7F583EFF00538E53870020537FE");
            list.Add(":1002200080D0E4F537F538C3E5389536E5379535B0");
            list.Add(":10023000501874402538F582E434E7F58374CDF026");
            list.Add(":100240000538E5387002053780DDAD367AE779404C");
            list.Add(":100250007EE77F40AB07AF32AE311208B8E490E6DC");
            list.Add(":100260008AF090E68BE536F02532F532E53535310A");
            list.Add(":10027000F531C3E5349536F534E5339535F533027C");
            list.Add(":1002800001BD90E6B8E064406003020398E51A708F");
            list.Add(":10029000051209678F1AE53445337003020398E4A9");
            list.Add(":1002A00090E68AF090E68BF090E6A0E020E1F990ED");
            list.Add(":1002B000E68BE0753500F53690E6B9E0B4A338E496");
            list.Add(":1002C000F537F538C3E5389536E5379535400302FF");
            list.Add(":1002D000037C74402538F582E434E7F583E0FFE5DC");
            list.Add(":1002E000322538F582E5313537F583EFF00538E50D");
            list.Add(":1002F000387002053780CDE4F537F538C3E5389519");
            list.Add(":0703000036E5379535507515");
            list.Add(":10030700851A39E51A64016044E5322538FFE5317D");
            list.Add(":100317003537FEE51A24FFFDE434FF5EFEEF5D4E40");
            list.Add(":100327006010E5322538FFE51A145FFFC3E51A9F11");
            list.Add(":10033700F539C3E5369538FFE5359537FEC3EF95B3");
            list.Add(":1003470039EE94005007C3E5369538F539E532257F");
            list.Add(":1003570038FFE5313537FE74402538F582E434E758");
            list.Add(":10036700AD82FCAB39120A9CE5392538F538E435FE");
            list.Add(":0303770037F53720");
            list.Add(":10037A008080E5362532F532E5353531F531C3E58C");
            list.Add(":0F038A00349536F534E5339535F533020296C3D5");
            list.Add(":010399002241");
            list.Add(":100C3200C0E0C083C08290E6B5E04401F0D2015327");
            list.Add(":0F0C420091EF90E65D7401F0D082D083D0E03264");
            list.Add(":100C9D00C0E0C083C0825391EF90E65D7404F0D044");
            list.Add(":060CAD0082D083D0E0328A");
            list.Add(":100CB300C0E0C083C0825391EF90E65D7402F0D030");
            list.Add(":060CC30082D083D0E03274");
            list.Add(":100B1900C0E0C083C08290E680E030E70E85080C13");
            list.Add(":100B290085090D85100E85110F800C85100C851116");
            list.Add(":100B39000D85080E85090F5391EF90E65D7410F04D");
            list.Add(":070B4900D082D083D0E0321E");
            list.Add(":100C8500C0E0C083C082D2035391EF90E65D740843");
            list.Add(":080C9500F0D082D083D0E032E0");
            list.Add(":100B5000C0E0C083C08290E680E030E70E85080CDC");
            list.Add(":100B600085090D85100E85110F800C85100C8511DF");
            list.Add(":100B70000D85080E85090F5391EF90E65D7420F006");
            list.Add(":070B8000D082D083D0E032E7");
            list.Add(":0109FF0032C5");
            list.Add(":010D70003250");
            list.Add(":010D7100324F");
            list.Add(":010D7200324E");
            list.Add(":010D7300324D");
            list.Add(":010D7400324C");
            list.Add(":010D7500324B");
            list.Add(":010D7600324A");
            list.Add(":010D77003249");
            list.Add(":010D78003248");
            list.Add(":010D79003247");
            list.Add(":010D7A003246");
            list.Add(":010D7B003245");
            list.Add(":010D7C003244");
            list.Add(":010D7D003243");
            list.Add(":010D7E003242");
            list.Add(":010D7F003241");
            list.Add(":010D80003240");
            list.Add(":010D8100323F");
            list.Add(":010D8200323E");
            list.Add(":010D8300323D");
            list.Add(":010D8400323C");
            list.Add(":010D8500323B");
            list.Add(":010D8600323A");
            list.Add(":010D87003239");
            list.Add(":010D88003238");
            list.Add(":010D89003237");
            list.Add(":010D8A003236");
            list.Add(":010D8B003235");
            list.Add(":010D8C003234");
            list.Add(":010D8D003233");
            list.Add(":010D8E003232");
            list.Add(":010D8F003231");
            list.Add(":010D90003230");
            list.Add(":010D9100322F");
            list.Add(":010D9200322E");
            list.Add(":100A00001201000200000040B404041000000102C2");
            list.Add(":100A100000010A06000200000040010009022E0049");
            list.Add(":100A200001010080320904000004FF0000000705F6");
            list.Add(":100A30000202000200070504020002000705860208");
            list.Add(":100A40000002000705880200020009022E000101D1");
            list.Add(":100A50000080320904000004FF00000007050202C4");
            list.Add(":100A60004000000705040240000007058602400020");
            list.Add(":100A70000007058802400000040309041003430036");
            list.Add(":100A80007900700072006500730073000E0345006A");
            list.Add(":0C0A90005A002D005500530042000000E9");
            list.Add(":100BB60090E682E030E004E020E60B90E682E0304A");
            list.Add(":100BC600E119E030E71590E680E04401F07F147EFD");
            list.Add(":0C0BD600001207AD90E680E054FEF02213");
            list.Add(":100B870030040990E680E0440AF0800790E680E0B0");
            list.Add(":100B97004408F07FDC7E051207AD90E65D74FFF038");
            list.Add(":0F0BA70090E65FF05391EF90E680E054F7F02274");
            list.Add(":1007AD008E3A8F3B90E600E054187012E53B240121");
            list.Add(":1007BD00FFE4353AC313F53AEF13F53B801590E698");
            list.Add(":1007CD0000E05418FFBF100BE53B25E0F53BE53A83");
            list.Add(":1007DD0033F53AE53B153BAE3A7002153A4E6005DE");
            list.Add(":0607ED00120C2180EE2237");
            list.Add(":020BE200A90761");
            list.Add(":100BE400AE14AF158F828E83A3E064037017AD013A");
            list.Add(":100BF40019ED7001228F828E83E07C002FFDEC3E84");
            list.Add(":080C0400FEAF0580DFE4FEFFF6");
            list.Add(":010C0C0022C5");
            list.Add(":100C0D0090E682E044C0F090E681F0438701000059");
            list.Add(":040C1D0000000022B1");
            list.Add(":100C21007400F58690FDA57C05A3E582458370F9E6");
            list.Add(":010C310022A0");
            list.Add(":03004300020800B0");
            list.Add(":03005300020800A0");
            list.Add(":10080000020C3200020CB300020C9D00020C8500A9");
            list.Add(":10081000020B1900020B50000209FF00020D7000CC");
            list.Add(":10082000020D7100020D7200020D7300020D7400C2");
            list.Add(":10083000020D7500020D7600020D7700020D7800A2");
            list.Add(":10084000020D7900020D7000020D7A00020D7B008E");
            list.Add(":10085000020D7C00020D7D00020D7E00020D7F0066");
            list.Add(":10086000020D8000020D7000020D7000020D70007C");
            list.Add(":10087000020D8100020D8200020D8300020D840032");
            list.Add(":10088000020D8500020D8600020D8700020D880012");
            list.Add(":10089000020D8900020D8A00020D8B00020D8C00F2");
            list.Add(":1008A000020D8D00020D8E00020D8F00020D9000D2");
            list.Add(":0808B000020D9100020D9200FF");
            list.Add(":0A0A9C008E3C8F3D8C3E8D3F8B4059");
            list.Add(":100AA600C28743B280120D58120D24120CC950048D");
            list.Add(":100AB600D2048059E519600FE53C90E679F0120CF6");
            list.Add(":100AC600C95004D2048046E53D90E679F0120CC97F");
            list.Add(":100AD6005004D2048037E4F541E541C395405021E6");
            list.Add(":100AE600053FE53FAE3E7002053E14F5828E83E07B");
            list.Add(":100AF60090E679F0120D145004D20480100541805E");
            list.Add(":100B0600D890E678E04440F0120C51C20453B27F0C");
            list.Add(":020B1600A20437");
            list.Add(":010B180022BA");
            list.Add(":0F0D240090E6787480F0E51725E090E679F022EC");
            list.Add(":100C5100120D58120D24120D1490E678E04440F064");
            list.Add(":0A0C6100120D5890E678E030E1E94A");
            list.Add(":010C6B002266");
            list.Add(":080D580090E678E020E6F922A4");
            list.Add(":0A08B8008E3C8F3D8D3E8A3F8B4041");
            list.Add(":1008C200120D58120D24120CC9500122E519600CA8");
            list.Add(":1008D200E53C90E679F0120CC9500122E53D90E624");
            list.Add(":1008E20079F0120CC950012290E6787480F0E51775");
            list.Add(":1008F20025E0440190E679F0120D1450012290E6B1");
            list.Add(":1009020079E0F541120D14500122E4F541E53E145F");
            list.Add(":10091200FFE541C39F501C90E679E0FFE540254189");
            list.Add(":10092200F582E4353FF583EFF0120D1450012205F4");
            list.Add(":100932004180DA90E6787420F0120D145001229072");
            list.Add(":10094200E679E0FFE5402541F582E4353FF583EFA6");
            list.Add(":10095200F0120D1450012290E6787440F090E6797E");
            list.Add(":04096200E0F541C3B8");
            list.Add(":01096600226E");
            list.Add(":0F0D140090E678E0FF30E0F8EF30E202D322C340");
            list.Add(":010D230022AD");
            list.Add(":100CC90090E678E0FF30E0F8EF30E202D322EF203F");
            list.Add(":050CD900E102D322C37B");
            list.Add(":010CDE0022F3");
            list.Add(":10096700E51970037F01227A107B407D40E4FFFE8A");
            list.Add(":100977001208B8E4F53A7400253AF582E43410F524");
            list.Add(":1009870083E53AF0053AE53AB440EB7C107D007B0D");
            list.Add(":1009970040E4FFFE120A9CE4F53AE53AF4FF7400DE");
            list.Add(":1009A700253AF582E43410F583EFF0053AE53AB4D9");
            list.Add(":1009B70040E87A107B007D40E4FFFE1208B89010F3");
            list.Add(":1009C70000E0F53AE53A30E005753B018008633A07");
            list.Add(":1009D7003F053A853A3BE4F53AE53AC3944050156A");
            list.Add(":1009E700AF3A7E007C107D40AB3B120A9CE53B256D");
            list.Add(":0709F7003AF53A80E4AF3B42");
            list.Add(":0109FE0022D6");
            list.Add(":030000000207F301");
            list.Add(":0C07F300787FE4F6D8FD75814102064DC8");
            list.Add(":100D0300EB9FF5F0EA9E42F0E99D42F0E89C45F046");
            list.Add(":010D130022BD");
            list.Add(":00000001FF");

        }



        private void initBut_Click(object sender, EventArgs e)
        { // other tab

        }

        private void funcVendorTransferData (
            String strDirection,
            String strReqCode,
            String strValue,
            String strIndex,
            int    intTbytes,
            String strOutBuffer
            )
        {
            if (curEndpt == null)
            {
                MessageBox.Show("Select <bulk> <iso> <int> endpoint enabled in the device tree.", "No endpoint selected");
                return;
            }

            int bytes = 0;

            try
            {
                bytes = intTbytes;
            }
            catch (Exception exc)
            {
                if (bytes < 1)
                {
                    //Just to remove warning
                    exc.ToString();

                    MessageBox.Show("Enter a valid number of bytes to transfer.", "Invalid Byte Count");
                    return;
                }
            }

            byte[] buffer = new byte[bytes];
            bool bXferCompleted = false;

            // Setting control endpt direction needs to occur before BuildDataCaption call
            CyControlEndPoint ctrlEpt = curEndpt as CyControlEndPoint;
            if (ctrlEpt != null)
                ctrlEpt.Direction = strDirection.Equals("In") ? CyConst.DIR_FROM_DEVICE : CyConst.DIR_TO_DEVICE;

            // Stuff the output buffer
            if (!curEndpt.bIn)
            {
                string[] hexTokens = strOutBuffer.Split(' ');

                int i = 0;

                //foreach (string tok in hexTokens)
                for (int j = 0; j < bytes; j++)
                {
                    string tok;

                    try
                    {
                        tok = hexTokens[j];
                    }
                    catch
                    {
                        tok = "";
                    }

                    if (tok.Length > 0)
                    {
                        try
                        {
                            buffer[i++] = (byte)Convert.ToInt32(tok, 16);
                        }
                        catch (Exception exc)
                        {
                            MessageBox.Show(exc.Message, "Input Error");
                            return;
                        }
                    }
                }
            }

            curEndpt.TimeOut = 2000;

            if (ctrlEpt != null)
            {
                ctrlEpt.Target = CyConst.TGT_DEVICE;
                ctrlEpt.ReqType = CyConst.REQ_VENDOR;

                ctrlEpt.Direction = strDirection.Equals("In") ? CyConst.DIR_FROM_DEVICE : CyConst.DIR_TO_DEVICE;

                try
                {
                    ctrlEpt.ReqCode = (byte)Convert.ToInt16(strReqCode, 16); //(byte)Util.HexToInt(ReqCodeBox.Text);
                    ctrlEpt.Value = (ushort)Convert.ToInt16(strValue, 16); //(ushort)Util.HexToInt(wValueBox.Text);
                    ctrlEpt.Index = (ushort)Convert.ToInt16(strIndex, 16); //(ushort)Util.HexToInt(wIndexBox.Text);
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message, "Input Error");
                    return;
                }

                bXferCompleted = ctrlEpt.XferData(ref buffer, ref bytes);


                if (bRecording && (script_stream != null))
                {
                    Xaction.ConfigNum = fx2.Config;
                    Xaction.IntfcNum = 0;
                    Xaction.AltIntfc = fx2.AltIntfc;
                    Xaction.EndPtAddr = ctrlEpt.Address;
                    Xaction.Tag = 0;

                    Xaction.bReqType = (byte)(ctrlEpt.Direction | ctrlEpt.ReqType | ctrlEpt.Target);
                    Xaction.CtlReqCode = ctrlEpt.ReqCode;
                    Xaction.wValue = ctrlEpt.Value;
                    Xaction.wIndex = ctrlEpt.Index;
                    Xaction.DataLen = (uint)bytes;
                    Xaction.Timeout = ctrlEpt.TimeOut / 1000;
                    Xaction.RecordSize = (uint)bytes + TTransaction.TotalHeaderSize;

                    //Write xaction and buffer
                    Xaction.WriteToStream(script_stream);
                    Xaction.WriteFromBuffer(script_stream, ref buffer, ref bytes);
                }
            }

            bool IsPkt = IsPacket.Checked ? true : false;

            CyBulkEndPoint bulkEpt = curEndpt as CyBulkEndPoint;
            if (bulkEpt != null)
            {
                bXferCompleted = bulkEpt.XferData(ref buffer, ref bytes, IsPkt);
                CheckForScripting(ref buffer, ref bytes);
            }
            bufferByteData = buffer;
        }

        private void funcBulkRead ()
        {
   
            if (bulkReadTag == null)
            {
                MessageBox.Show("bulkReadTag failure.", "No endpoint selected");
                return;
            }

            int bytes = 1024;

            byte[] buffer = new byte[bytes];
            bool bXferCompleted = false;

            // Setting control endpt direction needs to occur before BuildDataCaption call
            CyControlEndPoint ctrlEpt = curEndpt as CyControlEndPoint;
            if (ctrlEpt != null)
                ctrlEpt.Direction = CyConst.DIR_FROM_DEVICE;

            curEndpt.TimeOut = 2000;

            bool IsPkt = IsPacket.Checked ? true : false;
            
            CyBulkEndPoint bulkEpt = bulkReadTag as CyBulkEndPoint;
            if (bulkEpt != null)
            {
                bXferCompleted = bulkEpt.XferData(ref buffer, ref bytes, IsPkt);
                CheckForScripting(ref buffer, ref bytes);
            }

            bufferByteData = buffer;
            //DisplayXferData(buffer, bytes, bXferCompleted);

        }

        private void precisionCombox_SelectedIndexChanged(object sender, EventArgs e)
        {
            String strPrecsisonInput = "";
            // update global value
            strCurrentPrecision = precisionCombox.Text;

            // update dsp setting of precisions.
            switch (precisionCombox.Text)
            {
                case "x1":
                    strPrecsisonInput = "07 07";
                    gCntFramePackage = 344;
                    gf2.currentPrecision = (int)Precision.x1;
                    break;
                case "x2":
                    strPrecsisonInput = "06 06";
                    gCntFramePackage = 141;
                    gf2.currentPrecision = (int)Precision.x2;
                    break;
                case "x4":
                    strPrecsisonInput = "05 05";
                    gCntFramePackage = 83;
                    gf2.currentPrecision = (int)Precision.x4;
                    break;
                case "x8":
                    strPrecsisonInput = "04 04";
                    gCntFramePackage = 40;
                    gf2.currentPrecision = (int)Precision.x8;
                    break;
                case "x16":
                    strPrecsisonInput = "03 03";
                    gCntFramePackage = 34;
                    gf2.currentPrecision = (int)Precision.x16;
                    break;
                case "x32":
                    strPrecsisonInput = "02 02";
                    gCntFramePackage = 32;
                    gf2.currentPrecision = (int)Precision.x32;
                    break;
                default:
                    strPrecsisonInput = "07 07";
                    gCntFramePackage = 32;
                    gf2.currentPrecision = (int)Precision.x1;
                    break;
            }
            
            funcVendorTransferData("Out", "0xc0", "0x0000", "0x0000", 2, "62 62");
            funcVendorTransferData("Out", "0xc0", "0x0000", "0x0000", 2, "70 70");
            funcVendorTransferData("Out", "0xc4", "0x0000", "0x0000", 2, strPrecsisonInput);
            gf2.updateForm2();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            // show form 2 in a new form.
            gf2.updateForm2();
            gf2.Show(this);
        }

        private void Button_Init_Click(object sender, EventArgs e)
        {
            // test msg

            
            funcVendorTransferData("In", "0xf3", "0x0000", "0x0000", 512, "");
  

            // init 650
            funcVendorTransferData("In", "0xb8", "0x0000", "0x0000", 1, "");
                
            funcVendorTransferData("Out", "0xc0", "0x0000", "0x0000", 2, "62 62");

            funcVendorTransferData("Out", "0xc0", "0x0000", "0x0000", 2, "70 70");

            funcVendorTransferData("Out", "0xc4", "0x0000", "0x0000", 2, "05 05");

            for (int i = 0; i <= 1; i++)
            {
                funcVendorTransferData("In", "0xb8", "0x0000", "0x0000", 1, "");

                funcVendorTransferData("Out", "0xc0", "0x0000", "0x0000", 2, "62 62");
            }
            
            OutputBox.Text += Environment.NewLine + " Init 650 Machine Finished.\r\n";

        }

        private void buttom_A_package_Click(object sender, EventArgs e)
        {
            long tempData = 0;
            //int varBreakvalue = 10000;
            funcVendorTransferData("In", "0xb8", "0x0000", "0x0000", 1, "");
            funcVendorTransferData("In", "0xb3", "0x0000", "0x0000", 2, "");
            funcVendorTransferData("In", "0xb6", "0x0000", "0x0000", 1, "");

            //while (funcCheckReadReady());

            // read

            funcVendorTransferData("Out", "0xb5", "0x0000", "0x0000", 2, "02 00");
            funcBulkRead();

            for (int cntPkg = 0; cntPkg < 1024; cntPkg += 4)
            {
                tempData = (bufferByteData[cntPkg] << 24) |
                            (bufferByteData[cntPkg + 1] << 16) |
                            (bufferByteData[cntPkg + 2] << 8) |
                            (bufferByteData[cntPkg + 3]);
                OutputBox.Text +=   bufferByteData[cntPkg].ToString() + " "+
                                    bufferByteData[cntPkg+1].ToString() + " " +
                                    bufferByteData[cntPkg+2].ToString() + " " +
                                    bufferByteData[cntPkg+3].ToString() + " " +
                                    (double)(tempData / 150000000.0) + "\r\n ";  

            }

        }

        private bool funcCheckReadReady ()
        {
            bool result = true;
            funcVendorTransferData("In", "0xc2", "0x0000", "0x0000", 2, "");

            if (bufferByteData[1] == 0x02) result = false;
            return result;
        }

        private void buttFrame_Click(object sender, EventArgs e)
        {
            funcCatchAframe();
            
        }

        private void funcCatchAframe()
        {
            // init frame size by precision.
            int countFrameData = 0;
            int tempData = 0;
            bool flagCheckVaild = false;
            FileInfo fi = new FileInfo(fileName);
            // Check if file already exists. If yes, delete it.     
            if (fi.Exists)
            {
                fi.Delete();
            }
            StreamWriter sw = fi.CreateText();


            // init bulk read reg.
            funcVendorTransferData("In", "0xb8", "0x0000", "0x0000", 1, "");
            funcVendorTransferData("In", "0xb3", "0x0000", "0x0000", 2, "");
            funcVendorTransferData("In", "0xb6", "0x0000", "0x0000", 1, "");

            // read a frame by the size of precision
            for (int cnt = 0; cnt < gCntFramePackage - 1; cnt++)
            {
                while (funcCheckReadReady());
                funcVendorTransferData("Out", "0xb5", "0x0000", "0x0000", 2, "02 00");
                funcBulkRead();
                for (int cntPkg = 0; cntPkg < 1024; cntPkg += 4)
                {

                    tempData = (bufferByteData[cntPkg] << 24) |
                                (bufferByteData[cntPkg + 1] << 16) |
                                (bufferByteData[cntPkg + 2] << 8) |
                                (0x00);

                    gf2.dataFrame[cnt * 256 + cntPkg] = (tempData / 150000000.0);

                    // Create a new file     
                    sw.WriteLine("{0}",gf2.dataFrame[cnt * 256 + cntPkg]);
                   

                    if (tempData >= 45000000) flagCheckVaild = true;

                    countFrameData++;
                }
                /* debug msg
                OutputBox.Text += bufferByteData[0].ToString() + " " +
                                bufferByteData[1].ToString() + " " +
                                bufferByteData[2].ToString() + " " +
                                bufferByteData[3].ToString() + " " +
                                (double)(tempData / 150000000.0) + " " +
                                gf2.dataFrame[cnt*256] + "\r\n ";
                */
            }
            //OutputBox.Text += "This Frame Recived!\r\n";

            // update form 2
            if (flagCheckVaild)
            {
                gf2.updateForm2();
            }
            else
                MessageBox.Show("invalid data", "Please check the machine");

            sw.Close();
        }

        public void funcRenewForm2 ()
        {
            gf2 = new Form2(this);
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked) // automatically continue read frames 
            {
                gflagAutoFrame = true;
            } else
            {
                gflagAutoFrame = false;
            }
        }

        private void funcCallbackAutoReadFrame(Object source, ElapsedEventArgs e)
        {
            //OutputBox.Text += "...";
            gTimer.Stop();
           
            
            // prevent mult-thread crisis for timer callback usage.
            Invoke(new MethodInvoker(() =>
            {   
                if (gflagAutoFrame)
                    funcCatchAframe();
            }));

            gTimer.Start();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            DeviceTreeView.ExpandAll();
        }
    }
}