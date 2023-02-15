using IotHUbDevice;
using Microsoft.Azure.Devices.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent
{
    public class Program
    {
        static string[] Links = File.ReadAllLines("Logins.txt");
        static string OpcConnectionString = Links[1];
        static string DeviceConnectionString = Links[3];


        public static DateTime maintenanceDate = DateTime.MinValue;

        static async Task Main(string[] arguments)
        {
            using var deviceClient = DeviceClient.CreateFromConnectionString(DeviceConnectionString, TransportType.Mqtt);
            await deviceClient.OpenAsync();
            var device = new IoTDevice(deviceClient);

            await device.InitializeHandlers();

            DeviceOPC opcDevice = new DeviceOPC();

            DeviceOPC.StartConnection();

            var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync())
            {
                Console.WriteLine(DateTime.Now);
                await IoTDevice.SendTelemetry(DeviceOPC.client);
            }

            DeviceOPC.EndConnection();
            Console.ReadLine();
        }
    }
}
