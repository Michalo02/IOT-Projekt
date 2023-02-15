using Agent;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Opc.UaFx;
using Opc.UaFx.Client;
using System.Net.Mime;
using System.Net.Sockets;
using System.Text;

enum Errors
{
    EmergencyStop = 1,
    PowerFailure = 2,
    SensorFailue = 4,
    Unknown = 8
}

namespace IotHUbDevice
{
    public class IoTDevice
    {
        public static DeviceClient client;

        public IoTDevice(DeviceClient deviceClient)
        {
            client = deviceClient;
        }

        #region Sending telemetry
        public static async Task SendTelemetry(OpcClient opcClient)
        {
            var node = opcClient.BrowseNode(OpcObjectTypes.ObjectsFolder);

            if (node.Children().Count() > 1)
            {
                foreach (var childNode in node.Children())
                {
                    if (!childNode.DisplayName.Value.Contains("Server"))
                    {
                        var device = Convert.ToInt32(childNode.DisplayName.Value.Split(" ")[1]);

                        var productionStatus = opcClient.ReadNode($"ns=2;s=Device {device}/ProductionStatus").Value;
                        Console.WriteLine(productionStatus);
                        var workorderId = opcClient.ReadNode($"ns=2;s=Device {device}/WorkorderId").Value;
                        Console.WriteLine(workorderId);
                        var goodCount = opcClient.ReadNode($"ns=2;s=Device {device}/GoodCount").Value;
                        Console.WriteLine(goodCount);
                        var badCount = opcClient.ReadNode($"ns=2;s=Device {device}/BadCount").Value;
                        Console.WriteLine(badCount);
                        var temperature = opcClient.ReadNode($"ns=2;s=Device {device}/Temperature").Value;
                        Console.WriteLine(temperature);
                       

                        var telemetryData = new
                        {
                            device = device,
                            productionStatus = productionStatus,
                            workorderId = workorderId,
                            goodCount = goodCount,
                            badCount = badCount,
                            temperature = temperature,
                            
                        };

                        await SendTelemetryMessage(telemetryData, client);

                        var deviceErrors = opcClient.ReadNode($"ns=2;s=Device {device}/deviceErrors").Value;
                        Console.WriteLine($"Device errors = {deviceErrors}");
                        var productionRate = opcClient.ReadNode($"ns=2;s=Device {device}/ProductionRate").Value;
                        Console.WriteLine($"Production rate = {productionRate}");

                        await TwinAsync(deviceErrors,  productionRate);
                    }
                }
            }
            await Task.Delay(1000);
        }
        public static async Task SendTelemetryMessage(dynamic telemetryData, DeviceClient client)
        {
            var dataString = JsonConvert.SerializeObject(telemetryData);

            Microsoft.Azure.Devices.Client.Message eventMessage = new Microsoft.Azure.Devices.Client.Message(Encoding.UTF8.GetBytes(dataString));
            eventMessage.ContentType = MediaTypeNames.Application.Json;
            eventMessage.ContentEncoding = "utf-8";
            Console.WriteLine($"\t{DateTime.Now.ToLocalTime()}> Data: [{dataString}]");

            await client.SendEventAsync(eventMessage);
        }
        #endregion
        #region Receiving Message
        private async Task OnC2dMessageReceivedAsync(Message receivedMessage, object _)
        {
            Console.WriteLine($"\t{DateTime.Now}> C2D Message Callback - message received with Id = {receivedMessage.MessageId}");
            PrintMessage(receivedMessage);
            await client.CompleteAsync(receivedMessage);
            Console.WriteLine($"\t {DateTime.Now}> Completed C2D message with Id = {receivedMessage.MessageId}");
        }
        private void PrintMessage(Message receivedMessage)
        {
            string messageData = Encoding.ASCII.GetString(receivedMessage.GetBytes());
            Console.WriteLine($"\t\tReceived message: {messageData}");

            int propCount = 0;
            foreach (var prop in receivedMessage.Properties)
            {
                Console.WriteLine($"\t\tProperty[{propCount++}]> Key={prop.Key} : Value = {prop.Value}");
            }
        }
        #endregion
        #region TwinDevice
        public static async Task TwinAsync(dynamic deviceErrors, dynamic productionRate)
        {

            string errors = string.Empty;
            await UpdateTwinValueAsync("deviceErrors", deviceErrors);
            if ((deviceErrors & Convert.ToInt32(Errors.Unknown)) != 0)
            {
                errors += "Unknown, ";
            }
            if ((deviceErrors & Convert.ToInt32(Errors.SensorFailue)) != 0)
            {
                errors += "SensorFailure, ";
            }
            if ((deviceErrors & Convert.ToInt32(Errors.PowerFailure)) != 0)
            {
                errors += "PowerFailure, ";
            }
            if ((deviceErrors & Convert.ToInt32(Errors.EmergencyStop)) != 0)
            {
                errors += "Emergency stop";
            }

            await UpdateTwinValueAsync("productionRate", productionRate);
        }

        public static async Task UpdateTwinValueAsync(string valueName, dynamic value)
        {
            var twin = await client.GetTwinAsync();

            var reportedProperties = new TwinCollection();
            reportedProperties[valueName] = value;

            await client.UpdateReportedPropertiesAsync(reportedProperties);
        }

        public static async Task UpdateTwinValueAsync(string valueName, DateTime value)
        {
            var twin = await client.GetTwinAsync();

            var reportedProperties = new TwinCollection();
            reportedProperties[valueName] = value;

            await client.UpdateReportedPropertiesAsync(reportedProperties);
        }

        private async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            Console.WriteLine($"\tDesired property change:\n\t{JsonConvert.SerializeObject(desiredProperties)}");
            Console.WriteLine("\tSending current time as reported property");
            TwinCollection reportedProperties = new TwinCollection();
            reportedProperties["DateTimeLastDesiredPropertyChangeReceived"] = DateTime.Now;

            await client.UpdateReportedPropertiesAsync(reportedProperties).ConfigureAwait(false);
        }
        #endregion
        #region Direct Method
        async Task EmergencyStop(string deviceId)
        {
            DeviceOPC.Emergency_Stop(deviceId);
            await (Task.Delay(1000));
        }
        private async Task<MethodResponse> EmergencyStopHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"\tMETHOD EXECUTED: {methodRequest.Name}");

            var payload = JsonConvert.DeserializeAnonymousType(methodRequest.DataAsJson, new { machineId = default(string) });

            await EmergencyStop(payload.machineId);

            return new MethodResponse(0);
        }

        async Task ResetErrorStatus(string deviceId)
        {
            DeviceOPC.Reset_Errors(deviceId);
            await (Task.Delay(1000));
        }
        private async Task<MethodResponse> ResetErrorStatusHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"\tMETHOD EXECUTED: {methodRequest.Name}");

            var payload = JsonConvert.DeserializeAnonymousType(methodRequest.DataAsJson, new { machineId = default(string) });

            await ResetErrorStatus(payload.machineId);

            return new MethodResponse(0);
        }

        async Task Maintenance()
        {
            DeviceOPC.Maintenance();
            await (Task.Delay(1000));

        }
        private async Task<MethodResponse> MaintenanceHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"\tMETHOD EXECUTED: {methodRequest.Name}");

            await Maintenance();

            return new MethodResponse(0);
        }

        private static async Task<MethodResponse> DefaultServiceHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"\tMETHOD EXECUTED: {methodRequest.Name}");

            await Task.Delay(1000);

            return new MethodResponse(0);
        }
        #endregion
        #region initializer
        public async Task InitializeHandlers()
        {
            await client.SetReceiveMessageHandlerAsync(OnC2dMessageReceivedAsync, client);

            await client.SetMethodHandlerAsync("EmergencyStop", EmergencyStopHandler, client);
            await client.SetMethodHandlerAsync("ResetErrorStatus", ResetErrorStatusHandler, client);
            await client.SetMethodHandlerAsync("Maintenance", MaintenanceHandler, client);

            await client.SetMethodDefaultHandlerAsync(DefaultServiceHandler, client);

            await client.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, client);
        }
        #endregion
    }
}
