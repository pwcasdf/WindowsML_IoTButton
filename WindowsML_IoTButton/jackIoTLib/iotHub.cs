using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using System.Threading;


//****************************************************************
//
//to use this, Microsoft.Azure.Devices.Client nuget needs to be installed    @pwcasdf
//
//****************************************************************

namespace WindowsML_IoTButton.jackIoTLib
{
    class IotHub
    {
        // where SendDeviceClient should be passed like below,
        // DeviceClient any_name_here = DeviceClient.Create({IoT_Hub_URI}, new DeviceAuthenticationWithRegistrySymmetricKey({Device_ID}, {Device_Key}), TransportType.Mqtt);
        // message is what message needs to be sent
        // usage as belows
        //
        //
        // IotHub hub = new IotHub();
        // hub.SendMsgToHub(_DeviceClient, "gogogogo");
        //
        //
        // @pwcasdf
        public async void SendMsgToHub(DeviceClient _DeviceClient, string message)
        {
            await _DeviceClient.SendEventAsync(new Message(Encoding.ASCII.GetBytes(message)));
        }

        // where ReceiveDeviceClient should be passed like below, 
        // DeviceClient any_name_here = DeviceClient.Create({IoT_Hub_URI}, new DeviceAuthenticationWithRegistrySymmetricKey({Device_ID}, {Device_Key}), TransportType.Mqtt);
        // returns string type whatever received
        // usage as belows
        //
        //
        //private async void ReceivingMessage()
        //{
        //    Message ReceivedMessage = new Message();
        //    string OrderFromHub = null;

        //    while (true)
        //    {
        //        try
        //        {
        //            OrderFromHub = await hub.ReceiveMsgFromHub(_DeviceClient, ReceivedMessage);
        //        }
        //        catch
        //        {
        //            Debug.WriteLine("null value occured");
        //        }


        //        if (OrderFromHub == null)
        //            continue;

        //        testTB.Text = OrderFromHub;
        //    }
        //}
        //
        //
        // @pwcasdf

        public async Task<String> ReceiveMsgFromHub(DeviceClient _DeviceClient, Message ReceivedMessage)
        {
            ReceivedMessage = await _DeviceClient.ReceiveAsync();
    
            await _DeviceClient.CompleteAsync(ReceivedMessage);
            return Encoding.ASCII.GetString(ReceivedMessage.GetBytes());
        }
    }
}
