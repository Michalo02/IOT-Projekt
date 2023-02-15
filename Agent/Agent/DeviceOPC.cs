using Opc.UaFx.Client;
using Opc.UaFx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IotHUbDevice;

namespace Agent
{
    public class DeviceOPC
    {
        public static OpcClient client;

        public static void StartConnection()
        {
            client = new OpcClient(File.ReadAllLines("Logins.txt")[1]);
            client.Connect();
            Console.WriteLine("Connection Succes");
        }

        public static void EndConnection()
        {
            client.Disconnect();
        }

        public static async Task Emergency_Stop(string deviceId)
        {
            Console.WriteLine($"\tDevice shut down {deviceId}\n");
            client.CallMethod($"ns=2;s=Device {deviceId}", $"ns=2;s=Device {deviceId}/EmergencyStop");
            client.WriteNode($"ns=2;s=Device {deviceId}/ProductionRate", OpcAttribute.Value, 0);
            await Task.Delay(1000);
        }

        public static async Task Reset_Errors(string deviceId)
        {
            client.CallMethod($"ns=2;s=Device {deviceId}", $"ns=2;s=Device {deviceId}/ResetErrorStatus");
            await Task.Delay(1000);
        }

        public static async Task Maintenance()
        {
            Program.maintenanceDate = DateTime.Now;
            Console.WriteLine($"Device Last Maintenace Date set to: {Program.maintenanceDate}\n");
            await IoTDevice.UpdateTwinValueAsync("LastMaintenanceDate", Program.maintenanceDate);
        }
    }
}
