using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using HidLibrary;
using System.IO;

namespace ZetaCheckCore
{
    public partial class MainWindow : Window
    {
        // Sony PS4 Kolu Kimlikleri
        private const int SonyVendorId = 0x054C;
        private const int Ds4V1ProductId = 0x05C4;
        private const int Ds4V2ProductId = 0x09CC;
        
        private HidDevice ds4Device;

        public MainWindow()
        {
            InitializeComponent();
            InitializeWebViewAsync();
        }

        async void InitializeWebViewAsync()
        {
            await webView.EnsureCoreWebView2Async(null);

            // HTML dosyasının yolunu bul ve WebView'a yükle
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "index.html");
            webView.CoreWebView2.Navigate(htmlPath);

            // HTML'den gelen sürükleme ve kapatma komutlarını dinle
            webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;

            StartGamepadReader();
        }

        private void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string message = e.TryGetWebMessageAsString();
            
            if (message == "close")
            {
                Application.Current.Shutdown();
            }
            else if (message == "drag")
            {
                if (System.Windows.Input.Mouse.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                {
                    this.DragMove();
                }
            }
        }

        private void StartGamepadReader()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    if (ds4Device == null || !ds4Device.IsConnected)
                    {
                        ds4Device = HidDevices.Enumerate(SonyVendorId, Ds4V2ProductId).FirstOrDefault()
                                 ?? HidDevices.Enumerate(SonyVendorId, Ds4V1ProductId).FirstOrDefault();

                        if (ds4Device != null)
                        {
                            ds4Device.OpenDevice();
                        }
                    }

                    if (ds4Device != null && ds4Device.IsConnected)
                    {
                        var report = ds4Device.ReadReport(100); 
                        
                        if (report.ReadStatus == HidDeviceData.ReadStatus.Success)
                        {
                            byte[] data = report.Data;
                            
                            byte batteryByte = data[30];
                            int batteryLevel = batteryByte & 0x0F;
                            int batteryPercentage = Math.Min((batteryLevel * 100) / 11, 100);
                            bool isCharging = (batteryByte & 0x10) != 0;
                            
                            string connectionType = "USB";
                            double voltage = isCharging ? 4.15 : 3.75; 

                            Dispatcher.Invoke(() =>
                            {
                                string script = $"UpdateHardwareStats({batteryPercentage}, {isCharging.ToString().ToLower()}, '{connectionType}', {voltage.ToString(System.Globalization.CultureInfo.InvariantCulture)}, 45, 98);";
                                webView.CoreWebView2.ExecuteScriptAsync(script);
                            });
                        }
                    }
                    
                    await Task.Delay(500); 
                }
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            ds4Device?.CloseDevice();
            base.OnClosed(e);
        }
    }
}
