using System;
using System.Windows.Forms;
using TestRFIDApplication.RFIDClasses;
using log4net;

namespace TestRFIDApplication
{
    public partial class Form1 : Form
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Form1));

        private NotifyModeClass _reader;

        public Form1()
        {
            InitializeComponent();
            log4net.Config.XmlConfigurator.Configure();
        }

        private void btnStartListening_Click(object sender, EventArgs e)
        {

            if (!int.TryParse(txtPort.Text.Trim(), out int readerPort))
            {
                AppendLog("Invalid port number");
                log.Warn("Connect failed: Invalid port.");
                return;
            }

            try
            {
                _reader = new NotifyModeClass(readerPort);
                _reader.TagRead += OnTagRead;
                _reader.LogMessage += AppendLog;
                _reader.StartListening();
                AppendLog($"Listening started on port {readerPort}...");
                log.Info($"Listening started on port {readerPort}.");
            }
            catch (Exception ex)
            {
                AppendLog("Error: " + ex.Message);
                log.Error("Error while connecting to reader", ex);
            }
        }

        private void OnTagRead(string tagInfo)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action(() => AppendLog(tagInfo)));
            }
            else
            {
                AppendLog(tagInfo);
            }

            log.Info("Tag Read: " + tagInfo);
        }


        private void AppendLog(string message)
        {
            txtLog.AppendText($"{DateTime.Now:HH:mm:ss} - {message}{Environment.NewLine}");
        }

        private void btnStopListening_Click(object sender, EventArgs e)
        {
            if (_reader != null)
            {
                _reader.StopListening();
                //_reader.Dispose();
                AppendLog("Listening stopped.");
                log.Info("Listening stopped.");
                _reader = null;
            }
        }

    }
}


