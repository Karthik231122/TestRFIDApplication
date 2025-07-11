﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using FEDM;
using FEDM.Utility;
using FEDM.FunctionUnit;
using log4net;
using static FEDM.BrmItem;


namespace TestRFIDApplication.RFIDClasses
{
    internal class NotifyModeClass : IConnectListener, IReaderListener
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(NotificationReader));

        private ReaderModule m_reader;
        private readonly object _lock = new object();

        // *********************************************************
        // SETTINGS
        // *********************************************************
        private int _port;           // Set Port-Number
        private string ipAddr = "";     // (Default: any IPv4)
        private bool keepAlive = true;  // Set Keep-Alive on/off
        // *********************************************************

        public event Action<string> TagRead;
        public event Action<string> LogMessage;
        public NotifyModeClass(int port)
        {
            _port = port;
            m_reader = new ReaderModule(RequestMode.UniDirectional);
            m_reader.setReaderType(ReaderType.LRU500i_PoE);
            //m_extensionModule = new ExtensionModule(m_reader);
        }
        ~NotifyModeClass()
        {
            m_reader.Dispose();
        }
        public void StartListening()
        {
            lock (_lock)
            {
                //m_reader.brm().clearQueue();
                //m_reader.brm().setQueueMaxItemCount(0);

                int state = m_reader.async().startNotification(this);
                string msg1 = "startNotification: " + m_reader.lastErrorStatusText();
                if (state == ErrorCode.Ok)
                {
                    logger.Info(msg1);
                    LogMessage?.Invoke(msg1);
                }
                else
                {
                    logger.Error(msg1);
                    LogMessage?.Invoke("Error: " + msg1);
                    return;
                }

                state = m_reader.startListenerThread(ListenerParam.createTcpListenerParam(_port, ipAddr, keepAlive), this);
                string msg2 = "startListenerThread: " + m_reader.lastErrorStatusText();


                if (state == ErrorCode.Ok)
                {
                    logger.Info(msg2);
                    LogMessage?.Invoke(msg2);
                }
                else
                {
                    logger.Error(msg2);
                    LogMessage?.Invoke("Error: " + msg2);
                    return;
                }
            }
        }
        public void StopListening()
        {
            lock (_lock)
            {
                int state = m_reader.stopListenerThread();
                string msg1 = "stopListenerThread: " + m_reader.lastErrorStatusText();

                if (state == ErrorCode.Ok)
                {
                    logger.Info(msg1);
                    LogMessage?.Invoke(msg1);
                }
                else
                {
                    logger.Error(msg1);
                    LogMessage?.Invoke("Error: " + msg1);
                }

                state = m_reader.async().stopNotification();
                string msg2 = "stopNotification: " + m_reader.lastErrorStatusText();
                if (state == ErrorCode.Ok)
                {
                    logger.Info(msg2);
                    LogMessage?.Invoke(msg2);
                }
                else
                {
                    logger.Error(msg2);
                    LogMessage?.Invoke("Error: " + msg2);
                }
            }
        }
        public void onConnect(PeerInfo peerInfo)
        {
            lock (_lock)
            {
                string msg = "Reader connected at: " + peerInfo.ipAddress();
                logger.Info(msg);
                LogMessage?.Invoke(msg);
            }
        }
        public void onDisconnect()
        {
            lock (_lock)
            {
                string msg = "Reader disconnected";
                logger.Warn(msg);
                LogMessage?.Invoke(msg);
            }
        }
        public void onNewRequest()
        {
            lock (_lock)
            {
                int state;

                EventType eventType = m_reader.async().popEvent(out state);
                while (eventType != EventType.Invalid)
                {

                    string statusText = "IReaderListener: NewRequest";
                    string stateText = "State: " + ReaderStatus.toString(state);

                    logger.Debug(statusText);
                    LogMessage?.Invoke(statusText);

                    logger.Debug(stateText);
                    LogMessage?.Invoke(stateText);
                    switch (eventType)
                    {
                        case EventType.BrmEvent:
                            onNewBrmEvent();
                            break;

                        //case EventType.InputEvent:
                        //onNewInputEvent();
                        //break;

                        case EventType.DiagEvent:
                            onNewDiagStatusEvent();
                            break;

                        //case EventType.TagEvent:
                        //  onNewTagEvent();
                        //  break;

                        //case EventType.PeopleCounterEvent:
                        //  onNewPeopleCounterEvent();
                        //  break;

                        default:
                            // ignore all other event types
                            string ignoredMsg = "Ignored EventType: " + eventType;
                            logger.Debug(ignoredMsg);
                            LogMessage?.Invoke(ignoredMsg);
                            break;
                    }
                    eventType = m_reader.async().popEvent(out state);
                }
            }
        }

        private void onNewBrmEvent()
        {
            BrmItem brmItem = m_reader.brm().popItem();
            while (brmItem != null)
            {
                StringBuilder brmItemPrint = new StringBuilder();

                // **************
                // Date
                // **************
                if (brmItem.dateTime().isValidDate())
                {
                    int day = brmItem.dateTime().day();
                    int month = brmItem.dateTime().month();
                    int year = brmItem.dateTime().year();

                    brmItemPrint.Append("Date: " + year + "-" + month + "-" + day + "\n");
                }
                else
                {
                    brmItemPrint.Append("Date: " + "not valid" + "\n");
                }

                // **************
                // Time
                // **************

                if (brmItem.dateTime().isValidTime())
                {
                    int hour = brmItem.dateTime().hour();
                    int minute = brmItem.dateTime().minute();
                    int second = brmItem.dateTime().second();
                    int milliSecond = brmItem.dateTime().milliSecond();

                    brmItemPrint.Append("Time: " + hour + ":" + minute + ":" + second + "." + milliSecond + "\n");
                }
                else
                {
                    brmItemPrint.Append("Time: " + "not valid" + "\n");
                }

                // **************
                // IDD
                // **************
                if (brmItem.tag().isValid())
                {
                    brmItemPrint.Append("IDD: " + brmItem.tag().iddToHexString() + "\n");

                    // **************
                    // RSSI
                    // **************
                    List<RssiItem> list = brmItem.tag().rssiValues();
                    foreach (RssiItem rssiItem in list)
                    {
                        if (rssiItem.isValid())
                        {
                            brmItemPrint.Append("RSSI: " + rssiItem.rssi() + "\n");
                            brmItemPrint.Append("Phase Angle: " + rssiItem.phaseAngle() + "dec" + "\n");
                            brmItemPrint.Append("Antenna Number: " + rssiItem.antennaNumber() + "\n");
                        }
                        else
                        {
                            brmItemPrint.Append("RSSI: " + "not valid" + "\n");
                        }
                    }
                }
                else
                {
                    brmItemPrint.Append("IDD: " + "not valid" + "\n");
                }

                // **************
                // Data
                // **************
                if (brmItem.dataBlocks().isValid())
                {
                    brmItemPrint.Append("Data: " + HexConvert.toHexString(brmItem.dataBlocks().blocks(), " ") + "\n");
                    brmItemPrint.Append("Data blockCount: " + brmItem.dataBlocks().blockCount() + "\n");
                    brmItemPrint.Append("Data blockSize: " + brmItem.dataBlocks().blockSize() + "\n");
                }
                else
                {
                    brmItemPrint.Append("Data: " + "not valid" + "\n");
                }

                // **************
                // Antenna
                // **************
                if (brmItem.antennas().isValid())
                {
                    brmItemPrint.Append("Antenna: " + brmItem.antennas().antennas() + "\n");
                }
                else
                {
                    brmItemPrint.Append("Antenna: " + "not valid" + "\n");
                }

                // **************
                // MAC-Address
                // **************
                if (brmItem.mac().isValid())
                {
                    brmItemPrint.Append("MAC: " + HexConvert.toHexString(brmItem.mac().macAddress(), ":") + "\n");
                }
                else
                {
                    brmItemPrint.Append("MAC: " + "not valid" + "\n");
                }

                // **************
                // Input
                // **************

                if (brmItem.input().isValid())
                {
                    brmItemPrint.Append("Input: " + brmItem.input().input() + "\n");
                    brmItemPrint.Append("State: " + brmItem.input().state() + "\n");
                }
                else
                {
                    brmItemPrint.Append("Input: " + "not valid" + "\n");
                }

                // **************
                // Device/Scanner ID
                // **************
                if (brmItem.scannerId().isValid())
                {
                    if (brmItem.scannerId().type() == SectorScannerId.TypeDeviceId)
                    {
                        brmItemPrint.Append("Device ID: " + brmItem.scannerId().scannerId() + "\n");
                    }
                    if (brmItem.scannerId().type() == SectorScannerId.TypeScannerId)
                    {
                        brmItemPrint.Append("Scanner ID: " + brmItem.scannerId().scannerId() + "\n");
                    }

                }
                else
                {
                    brmItemPrint.Append("Device/Scanner ID: " + "not valid" + "\n");
                }

                // **************
                // Direction
                // **************

                if (brmItem.direction().isValid())
                {

                    if (brmItem.direction().isDetectionDisabled())
                    {
                        brmItemPrint.Append("Sector Direction: " + "Detection Disabled" + "\n");
                    }
                    if (brmItem.direction().isDirection1())
                    {
                        brmItemPrint.Append("Sector Direction: " + "Direction 1" + "\n");
                    }
                    if (brmItem.direction().isDirection2())
                    {
                        brmItemPrint.Append("Sector Direction: " + "Direction 2" + "\n");
                    }

                }
                else
                {
                    brmItemPrint.Append("Sector Direction: " + "not valid");
                }
                logger.Info(brmItemPrint.ToString());
                LogMessage?.Invoke(brmItemPrint.ToString());
                brmItem = m_reader.brm().popItem();
            }

        }

        private void onNewIdentificationEvent()
        {
            ReaderIdentification readerIdent = m_reader.identification();
            if (readerIdent.isValid())
            {
                logger.Info("*************************************");
                LogMessage?.Invoke("*************************************");

                logger.Info("Identification");
                LogMessage?.Invoke("Identification");

                string deviceId = "Reader device ID: " + readerIdent.deviceIdToHexString();
                logger.Info(deviceId);
                LogMessage?.Invoke(deviceId);

                string readerType = "Reader type: " + readerIdent.readerTypeToString();
                logger.Info(readerType);
                LogMessage?.Invoke(readerType);

                string firmware = "Firmware version: " + readerIdent.versionToString();
                logger.Info(firmware);
                LogMessage?.Invoke(firmware);

                logger.Info("*************************************");
                LogMessage?.Invoke("*************************************");
            }
            else
            {
                string msg = "Identification not valid";
                logger.Warn(msg);
                LogMessage?.Invoke(msg);
            }

        }
        private void onNewDiagStatusEvent()
        {
            DiagEventItem readerDiag = m_reader.diagnostic().popItem();
            while (readerDiag != null)
            {
                logger.Info("*************************************");
                LogMessage?.Invoke("*************************************");

                logger.Info("Diagnostic Status Event");
                LogMessage?.Invoke("Diagnostic Status Event");

                string report = "Report: \n" + readerDiag.toReport();
                logger.Info(report);
                LogMessage?.Invoke(report);

                logger.Info("*************************************");
                LogMessage?.Invoke("*************************************");

                if (readerDiag.warning().hfImpedance().isOn())
                {
                    string warning = "⚠️ Impedance error <> 50 Ohm\nPossible reasons:\n - No antenna connected\n - Metal too close to antenna\n - Bad cable or mismatch";
                    logger.Warn(warning);
                    LogMessage?.Invoke(warning);
                }

                if (readerDiag.warning().hfNoise().isOn())
                {
                    string warning = "⚠️ High noise level\nPossible reasons:\n - Another antenna too close\n - Cable too close\n - Nearby interference source";
                    logger.Warn(warning);
                    LogMessage?.Invoke(warning);
                }

                readerDiag = m_reader.diagnostic().popItem();
            }
        }
        private void onNewInputEvent()
        {
            InputEventItem inItem = m_reader.io().popInItem();
            while (inItem != null)
            {
                logger.Info("*************************************");
                LogMessage?.Invoke("*************************************");

                logger.Info("Input Event");
                LogMessage?.Invoke("Input Event");

                string time = "Time:\t" + inItem.dateTime().toString();
                logger.Info(time);
                LogMessage?.Invoke(time);

                string currentInput = "Current Input:\t" + inItem.currentInput();
                logger.Info(currentInput);
                LogMessage?.Invoke(currentInput);

                string previousInput = "Previous Input:\t" + inItem.previousInput();
                logger.Info(previousInput);
                LogMessage?.Invoke(previousInput);

                logger.Info("*************************************");
                LogMessage?.Invoke("*************************************");

                inItem = m_reader.io().popInItem();
            }
        }
        private void onNewTagEvent()
        {
            TagEventItem tagItem = m_reader.tagEvent().popItem();
            while (tagItem != null)
            {
                logger.Info("*************************************");
                LogMessage?.Invoke("*************************************");

                logger.Info("Tag Event");
                LogMessage?.Invoke("Tag Event");

                string tagDetails = TagEventToString(tagItem);
                logger.Info(tagDetails);
                LogMessage?.Invoke(tagDetails);

                logger.Info("*************************************");
                LogMessage?.Invoke("*************************************");

                tagItem = m_reader.tagEvent().popItem();
            }
        }

        private static string TagEventToString(TagEventItem tagEventItem)
        {
            StringBuilder tagEventItemPrint = new StringBuilder();

            // **************
            // Date
            // **************
            if (tagEventItem.dateTime().isValidDate())
            {
                int day = tagEventItem.dateTime().day();
                int month = tagEventItem.dateTime().month();
                int year = tagEventItem.dateTime().year();

                tagEventItemPrint.Append("Date: " + year + "-" + month + "-" + day + "\n");
            }
            else
            {
                tagEventItemPrint.Append("Date: " + "not valid" + "\n");
            }

            // **************
            // Time
            // **************

            if (tagEventItem.dateTime().isValidTime())
            {
                int hour = tagEventItem.dateTime().hour();
                int minute = tagEventItem.dateTime().minute();
                int second = tagEventItem.dateTime().second();
                int milliSecond = tagEventItem.dateTime().milliSecond();

                tagEventItemPrint.Append("Time: " + hour + ":" + minute + ":" + second + "." + milliSecond + "\n");
            }
            else
            {
                tagEventItemPrint.Append("Time: " + "not valid" + "\n");
            }

            // **************
            // IDD
            // **************
            if (tagEventItem.tag().isValid())
            {
                tagEventItemPrint.Append("IDD: " + tagEventItem.tag().iddToHexString() + "\n");

                // **************
                // RSSI + Antenna
                // **************
                List<RssiItem> list = tagEventItem.tag().rssiValues();
                foreach (RssiItem rssiItem in list)
                {
                    if (rssiItem.isValid())
                    {
                        tagEventItemPrint.Append("RSSI: " + rssiItem.rssi() + "\n");
                        tagEventItemPrint.Append("Antenna: " + rssiItem.antennaNumber() + "\n");
                    }
                    else
                    {
                        tagEventItemPrint.Append("RSSI: " + "not valid" + "\n");
                        tagEventItemPrint.Append("Antenna: " + "not valid" + "\n");
                    }

                    tagEventItemPrint.Append("Phase Angle: " + rssiItem.phaseAngle() + "dec" + "\n");

                }
            }
            else
            {
                tagEventItemPrint.Append("IDD: " + "not valid" + "\n");
            }

            // **************
            // Data
            // **************
            if (tagEventItem.dbUser().isValid())
            {
                tagEventItemPrint.Append("Data: " + HexConvert.toHexString(tagEventItem.dbUser().blocks()) + "\n");
                tagEventItemPrint.Append("Data blockCount: " + tagEventItem.dbUser().blockCount() + "\n");
                tagEventItemPrint.Append("Data blockSize: " + tagEventItem.dbUser().blockSize() + "\n");
            }
            else
            {
                tagEventItemPrint.Append("User Data Blocks not valid" + "\n");
            }

            if (tagEventItem.dbEpc().isValid())
            {
                tagEventItemPrint.Append("Data: " + HexConvert.toHexString(tagEventItem.dbEpc().blocks()) + "\n");
                tagEventItemPrint.Append("Data blockCount: " + tagEventItem.dbEpc().blockCount() + "\n");
                tagEventItemPrint.Append("Data blockSize: " + tagEventItem.dbEpc().blockSize() + "\n");
            }
            else
            {
                tagEventItemPrint.Append("EPC Data Blocks not valid" + "\n");
            }

            if (tagEventItem.dbTid().isValid())
            {
                tagEventItemPrint.Append("Data: " + HexConvert.toHexString(tagEventItem.dbTid().blocks()) + "\n");
                tagEventItemPrint.Append("Data blockCount: " + tagEventItem.dbTid().blockCount() + "\n");
                tagEventItemPrint.Append("Data blockSize: " + tagEventItem.dbTid().blockSize() + "\n");
            }
            else
            {
                tagEventItemPrint.Append("TID Data Blocks not valid" + "\n");
            }

            // **************
            // AFI
            // **************
            if (tagEventItem.tag().iso15693_IsValidAfi())
            {
                tagEventItemPrint.Append("AFI : " + tagEventItem.tag().iso15693_Afi() + "\n");
                tagEventItemPrint.Append("new AFI : " + tagEventItem.tag().iso15693_NewAfi() + "\n");
            }
            else
            {
                tagEventItemPrint.Append("AFI: " + "not valid" + "\n");
            }

            // **************
            // Input
            // **************

            if (tagEventItem.input().isValid())
            {
                tagEventItemPrint.Append("Input: " + tagEventItem.input().input() + "\n");
                tagEventItemPrint.Append("State: " + tagEventItem.input().state() + "\n");
            }
            else
            {
                tagEventItemPrint.Append("Input: " + "not valid" + "\n");
            }


            // **************
            // Direction
            // **************

            if (tagEventItem.direction().isValid())
            {
                tagEventItemPrint.Append("Sector Direction: " + "Direction " + tagEventItem.direction().direction() + "\n");
            }
            else
            {
                tagEventItemPrint.Append("Sector Direction: " + "not valid" + "\n");
            }

            // **************
            // EAS Alarm
            // **************

            if (!tagEventItem.evtSignals().isEmpty())
            {
                tagEventItemPrint.Append("EAS Alarm: " + tagEventItem.evtSignals().isEasAlarm() + "\n");
            }
            else
            {
                tagEventItemPrint.Append("No EAS Alarm" + "\n");
            }

            return tagEventItemPrint.ToString();
        }
    }
}
