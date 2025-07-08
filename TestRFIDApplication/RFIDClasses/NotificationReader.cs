using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using FEDM;
using FEDM.Utility;
using FEDM.FunctionUnit;
using log4net;

namespace TestRFIDApplication.RFIDClasses
{
    public class NotificationReader : IConnectListener, IReaderListener, IDisposable
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(NotificationReader));

        private ReaderModule m_reader;
        private ExtensionModule m_extensionModule;
        private readonly object _lock = new object();

        private int port;

        public event Action<string> TagRead;
        public event Action<string> LogMessage;


        public NotificationReader(int portNumber)
        {
            port = portNumber;

            m_reader = new ReaderModule(RequestMode.UniDirectional);
            m_extensionModule = new ExtensionModule(m_reader);

            string message = $"Initialized NotificationReader with Port: {portNumber}";
            logger.Info(message);
            LogMessage?.Invoke(message);
        }

        ~NotificationReader()
        {
            try
            {
                if (m_extensionModule != null)
                {
                    m_extensionModule.Dispose();
                    m_extensionModule = null;
                }

                if (m_reader != null)
                {
                    m_reader.Dispose();
                    m_reader = null;
                }

                logger.Info("Disposed NotificationReader class from finalizer.");
                LogMessage?.Invoke("Disposed NotificationReader class from finalizer.");
            }
            catch (Exception ex)
            {
                logger.Warn("Exception during finalizer dispose: ", ex);
            }
        }

        public void StartListening()
        {
            lock (_lock)
            {
                var funit = m_extensionModule.funit();
                logger.Info("Funit summary:\n" + funit.summary());
                LogMessage?.Invoke("Funit summary:\n" + funit.summary());
                logger.Info("Total antennas found (all types): " + funit.allAntennas().Count);
                LogMessage?.Invoke("Total antennas found (all types): " + funit.allAntennas().Count);

                int rfChannelCount = funit.readerRfChannelCount();

                logger.Info($"Detected RF Channels: {rfChannelCount}");
                LogMessage?.Invoke($"Detected RF Channels: {rfChannelCount}");


                for (int channel = 0; channel < rfChannelCount; channel++)
                {
                    var antenna = funit.createHfAntDynamic(channel);

                    if (antenna == null)
                    {
                        logger.Warn($"createHfAntDynamic returned null for channel {channel}");
                        LogMessage?.Invoke($"createHfAntDynamic returned null for channel {channel}");
                        continue;
                    }

                    int detectResult = antenna.detect();
                    logger.Info($"Antenna detect result on channel {channel}: {detectResult}");
                    LogMessage?.Invoke($"Antenna detect result on channel {channel}: {detectResult}");

                    if (detectResult == ErrorCode.Ok)
                    {
                        m_reader.rf().on(channel);
                        logger.Info($"RF ON for antenna on channel {channel}");
                        LogMessage?.Invoke($"RF ON for antenna on channel {channel}");

                        var values = new FunitHfAntValues();
                        int valueResult = antenna.getAntennaValues(values);

                        if (valueResult == ErrorCode.Ok && values.isValid())
                        {
                            logger.Info($"Antenna {channel} values:");
                            LogMessage?.Invoke($"Antenna {channel} values:");

                            logger.Info($" - C1: {values.c1()}, State: {values.c1TuningState()}");
                            LogMessage?.Invoke($" - C1: {values.c1()}, State: {values.c1TuningState()}");

                            logger.Info($" - C2: {values.c2()}, State: {values.c2TuningState()}");
                            LogMessage?.Invoke($" - C2: {values.c2()}, State: {values.c2TuningState()}");

                            logger.Info($" - R: {values.r()}, Phi: {values.phi()}");
                            LogMessage?.Invoke($" - R: {values.r()}, Phi: {values.phi()}");

                            logger.Info($" - Capacities OK: {values.areCapacitiesOk()}");
                            LogMessage?.Invoke($" - Capacities OK: {values.areCapacitiesOk()}");

                            logger.Info($" - Tuning State: {values.tuningState()}");
                            LogMessage?.Invoke($" - Tuning State: {values.tuningState()}");
                        }
                        else
                        {
                            logger.Warn($"Failed to read valid antenna values for channel {channel}");
                            LogMessage?.Invoke($"Failed to read valid antenna values for channel {channel}");
                        }
                    }
                    else
                    {
                        logger.Warn($"No antenna detected or error on channel {channel}");
                        LogMessage?.Invoke($"No antenna detected or error on channel {channel}");
                    }
                }


                m_reader.brm().clearQueue();
                m_reader.brm().setQueueMaxItemCount(0);
                int state = m_reader.async().startNotification(this);
                string msg1 = "startNotification: " + m_reader.lastErrorStatusText();
                logger.Info(msg1);
                LogMessage?.Invoke(msg1);

                if (state != ErrorCode.Ok)
                {
                    logger.Error("Failed to start notification.");
                    LogMessage?.Invoke("Failed to start notification.");
                    return;
                }

                state = m_reader.startListenerThread(ListenerParam.createTcpListenerParam(port), this);
                string msg2 = "startListenerThread: " + m_reader.lastErrorStatusText();
                //MessageBox.Show(msg2, "Listener Status");
                logger.Info(msg2);
                LogMessage?.Invoke(msg2);

                if (state != ErrorCode.Ok)
                {
                    logger.Error("Failed to start listener thread.");
                    LogMessage?.Invoke("Failed to start listener thread.");
                    return;
                }
            }
        }

        public void StopListening()
        {
            lock (_lock)
            {
                if (m_reader != null)
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
                        LogMessage?.Invoke("Error:" + msg1);

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
                        LogMessage?.Invoke("Error:" + msg2);
                    }

                }
                if (m_extensionModule != null)
                {
                    try
                    {
                        m_extensionModule.Dispose();
                        logger.Warn("Extension module disposed");
                        LogMessage?.Invoke("Extension module disposed");
                    }
                    catch (Exception ex)
                    {
                        logger.Warn("Exception while disposing ExtensionModule", ex);
                        LogMessage?.Invoke("Error disposing ExtensionModule: " + ex.Message);
                    }
                    m_extensionModule = null;
                }

                if (m_reader != null)
                {
                    try
                    {
                        m_reader.Dispose();
                        logger.Warn("Reader module disposed");
                        LogMessage?.Invoke("Reader module disposed");
                    }
                    catch (Exception ex)
                    {
                        logger.Warn("Exception while disposing ReaderModule", ex);
                        LogMessage?.Invoke("Error disposing ReaderModule: " + ex.Message);
                    }
                    m_reader = null;
                }


            }
        }


        public void onConnect(PeerInfo peerInfo)
        {
            string msg = "Reader connected at: " + peerInfo.ipAddress();
            logger.Info(msg);
            LogMessage?.Invoke(msg);

        }

        public void onDisconnect()
        {
            string msg = "Reader disconnected";
            logger.Warn(msg);
            LogMessage?.Invoke(msg);

        }

        public void onNewRequest()
        {
            lock (_lock)
            {
                int state;
                EventType eventType = m_reader.async().popEvent(out state);
                while (eventType != EventType.Invalid)
                {
                    logger.Debug("Received event type: " + eventType);
                    LogMessage?.Invoke("Received event type: " + eventType);
                    switch (eventType)
                    {
                        case EventType.TagEvent:
                            onNewTagEvent();
                            break;

                        case EventType.DiagEvent:
                            HandleDiagEvent();
                            break;

                        case EventType.BrmEvent:
                            HandleBrmEvent();
                            break;

                        default:
                            logger.Debug("Unhandled event type: " + eventType);
                            LogMessage?.Invoke("Unhandled event type: " + eventType);
                            break;
                    }
                    eventType = m_reader.async().popEvent(out state);
                }
            }
        }
        private void HandleDiagEvent()
        {
            DiagEventItem diagItem = m_reader.diagnostic().popItem();

            while (diagItem != null)
            {
                logger.Info("----- DiagEvent Received -----");
                LogMessage?.Invoke("----- DiagEvent Received -----");

                var dt = diagItem.dateTime();
                if (dt.isValidDate() || dt.isValidTime())
                {
                    logger.Info($"Timestamp: {dt.year()}-{dt.month():D2}-{dt.day():D2} {dt.hour():D2}:{dt.minute():D2}:{dt.second():D2}");
                    LogMessage?.Invoke($"Timestamp: {dt.year()}-{dt.month():D2}-{dt.day():D2} {dt.hour():D2}:{dt.minute():D2}:{dt.second():D2}");
                }

                var alert = diagItem.alert();
                if (alert != null && alert.isValid() && alert.isAnyAlert())
                {
                    logger.Warn("Alerts: " + alert.toReport());
                    LogMessage?.Invoke("Alerts: " + alert.toReport());
                }

                var warning = diagItem.warning();
                if (warning != null && warning.isValid() && warning.isAnyWarning())
                {
                    logger.Warn("Warnings: " + warning.toReport());
                    LogMessage?.Invoke("Warnings: " + warning.toReport());
                }

                var error = diagItem.error();
                if (error != null && error.isValid() && error.isAnyError())
                {
                    logger.Error("Errors: " + error.toReport());
                    LogMessage?.Invoke("Errors: " + error.toReport());
                }

                var antennas = diagItem.hfState();
                if (antennas != null && antennas.Count > 0)
                {
                    foreach (var ant in antennas)
                    {
                        if (ant.isValid())
                        {
                            logger.Info($"Antenna {ant.address()}: State = {ant.state()}, Details = {ant.toReport()}");
                            LogMessage?.Invoke($"Antenna {ant.address()}: State = {ant.state()}, Details = {ant.toReport()}");
                        }
                    }
                }

                logger.Info("----- End DiagEvent -----");
                LogMessage?.Invoke("----- End DiagEvent -----");

                diagItem = m_reader.diagnostic().popItem();
            }
        }

        private void HandleBrmEvent()
        {
            var tagItem = m_reader.brm().popItem();

            while (tagItem != null)
            {
                StringBuilder brmLog = new StringBuilder();

                if (tagItem.dateTime().isValidDate() || tagItem.dateTime().isValidTime())
                {
                    brmLog.AppendLine($"Timestamp: {tagItem.dateTime().year()}-{tagItem.dateTime().month():D2}-{tagItem.dateTime().day():D2} " +
                                      $"{tagItem.dateTime().hour():D2}:{tagItem.dateTime().minute():D2}:{tagItem.dateTime().second():D2}");
                }

                if (tagItem.tag().isValid())
                {
                    brmLog.AppendLine($"Tag ID: {tagItem.tag().iddToHexString()}");

                    var rssiList = tagItem.tag().rssiValues();
                    foreach (var rssi in rssiList)
                    {
                        if (rssi.isValid())
                        {
                            brmLog.AppendLine($"RSSI: {rssi.rssi()}, Antenna: {rssi.antennaNumber()}");
                        }
                    }
                }
                else
                {
                    brmLog.AppendLine("Invalid tag data in BRM event.");
                }

                logger.Info("BRM Event:\n" + brmLog.ToString());
                LogMessage?.Invoke("BRM Event:\n" + brmLog.ToString());

                tagItem = m_reader.brm().popItem();
            }
        }

        private void onNewTagEvent()
        {
            TagEventItem tagItem = m_reader.tagEvent().popItem();
            while (tagItem != null)
            {
                string output = FormatTag(tagItem);
                TagRead?.Invoke(output);
                logger.Info("Tag read:\n" + output);
                LogMessage?.Invoke("Tag read:\n" + output);
                tagItem = m_reader.tagEvent().popItem();
            }
        }

        public void Dispose()
        {
            StopListening();
            GC.SuppressFinalize(this);
        }
        private string FormatTag(TagEventItem tagEventItem)
        {
            StringBuilder tagEventItemPrint = new StringBuilder();

            if (tagEventItem.dateTime().isValidDate())
            {
                tagEventItemPrint.Append("Date: ")
                    .Append(tagEventItem.dateTime().year()).Append("-")
                    .Append(tagEventItem.dateTime().month()).Append("-")
                    .Append(tagEventItem.dateTime().day()).Append("\n");
            }

            if (tagEventItem.dateTime().isValidTime())
            {
                tagEventItemPrint.Append("Time: ")
                    .Append(tagEventItem.dateTime().hour()).Append(":")
                    .Append(tagEventItem.dateTime().minute()).Append(":")
                    .Append(tagEventItem.dateTime().second()).Append("\n");
            }

            if (tagEventItem.tag().isValid())
            {
                tagEventItemPrint.Append("Tag ID: ")
                    .Append(tagEventItem.tag().iddToHexString()).Append("\n");

                List<RssiItem> rssiList = tagEventItem.tag().rssiValues();
                foreach (var rssi in rssiList)
                {
                    if (rssi.isValid())
                    {
                        tagEventItemPrint.Append("RSSI: ")
                            .Append(rssi.rssi()).Append(", Antenna: ")
                            .Append(rssi.antennaNumber()).Append("\n");
                    }
                }
            }

            if (tagEventItem.direction().isValid())
            {
                tagEventItemPrint.Append("Direction: ")
                    .Append(tagEventItem.direction().direction()).Append("\n");
            }
            else
            {
                tagEventItemPrint.Append("Direction: not valid\n");
            }

            return tagEventItemPrint.ToString();
        }
    }
}
