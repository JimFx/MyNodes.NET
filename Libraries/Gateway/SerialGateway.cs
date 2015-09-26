﻿/*  MyNetSensors 
    Copyright (C) 2015 Derwish <derwish.pro@gmail.com>
    License: http://www.gnu.org/licenses/gpl-3.0.txt  
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyNetSensors.Gateway
{
    public delegate void MessageEventHandler(Message message);
    public delegate void NodeEventHandler(Node node);
    public delegate void SensorEventHandler(Sensor sensor);
    public delegate void DebugMessageEventHandler(string message);
    public delegate void ExceptionEventHandler(Exception exception);

    public class SerialGateway
    {
        private IComPort serialPort;
        public bool storeMessages = true;
        public bool enableAutoAssignId = true;

        public event MessageEventHandler OnMessageRecievedEvent;
        public event MessageEventHandler OnMessageSendEvent;
        public event NodeEventHandler OnNewNodeEvent;
        public event NodeEventHandler OnNodeUpdatedEvent;
        public event NodeEventHandler OnNodeLastSeenUpdatedEvent;
        public event NodeEventHandler OnNodeBatteryUpdatedEvent;
        public event SensorEventHandler OnNewSensorEvent;
        public event SensorEventHandler OnSensorUpdatedEvent;
        public event EventHandler OnClearNodesListEvent;
        public event EventHandler OnDisconnectedEvent;
        public event EventHandler OnConnectedEvent;
        public event DebugMessageEventHandler OnDebugTxRxMessage;
        public event DebugMessageEventHandler OnDebugGatewayStateMessage;

        public MessagesLog messagesLog = new MessagesLog();
        private List<Node> nodes = new List<Node>();
        private bool isConnected;

        private void DebugTxRx(string message)
        {
            if (OnDebugTxRxMessage != null)
                OnDebugTxRxMessage(message);
        }

        private void DebugGatewayState(string message)
        {
            if (OnDebugGatewayStateMessage != null)
                OnDebugGatewayStateMessage(message);
        }

        public void Connect(IComPort serialPort)
        {
            if (isConnected)
                Disconnect();

            this.serialPort = serialPort;
            this.serialPort.OnDataReceivedEvent += RecieveSerialMessage;
            this.serialPort.OnDisconnectedEvent += OnSerialPortDisconnectedEvent;
            isConnected = true;

            DebugGatewayState(String.Format("Gateway connected."));

            if (OnConnectedEvent != null)
                OnConnectedEvent(this, null);
        }

        public void Disconnect()
        {
            isConnected = false;
            if (serialPort != null)
            {
                serialPort.OnDataReceivedEvent -= RecieveSerialMessage;
                serialPort.OnDisconnectedEvent -= OnSerialPortDisconnectedEvent;
                serialPort = null;
            }

            DebugGatewayState(String.Format("Gateway disconnected."));


            if (OnDisconnectedEvent != null)
                OnDisconnectedEvent(this,null);
        }

        public bool IsConnected()
        {
            return (isConnected && serialPort.IsConnected());
        }

        private void OnSerialPortDisconnectedEvent(object sender, EventArgs e)
        {
            Disconnect();
        }


        private void SendMessage(string message)
        {
            if (!isConnected)
            {
                throw new Exception("Can`t send message. Serial port is not connected.");
                return;
            }

            serialPort.SendMessage(message);
        }



        public void SendMessage(Message message)
        {
            message.incoming = false;

            if (OnMessageSendEvent != null)
                OnMessageSendEvent(message);

            DebugTxRx(String.Format("TX: {0}", message.ToString()));

            string mes = String.Format("{0};{1};{2};{3};{4};{5}\n",
                message.nodeId,
                message.sensorId,
                (int)message.messageType,
                (message.ack) ? "1" : "0",
                message.subType,
                message.payload);

            SendMessage(mes);

            if (storeMessages)
                messagesLog.AddNewMessage(message);
        }


        private void RecieveSerialMessage(string message)
        {

            Message mes = ParseMessageFromString(message);
            mes.incoming = true;

            if (storeMessages)
                messagesLog.AddNewMessage(mes);

            if (OnMessageRecievedEvent != null)
                OnMessageRecievedEvent(mes);

            DebugTxRx(String.Format("RX: {0}", mes.ToString()));

            if (mes.isValid)
            {
                //Gateway ready
                if (mes.messageType == MessageType.C_INTERNAL
                    && mes.subType == (int)InternalDataType.I_GATEWAY_READY)
                    return;


                //Gateway log message
                if (mes.messageType == MessageType.C_INTERNAL
                    && mes.subType == (int)InternalDataType.I_LOG_MESSAGE)
                    return;

                //New ID request
                if (mes.nodeId == 255)
                {
                    if (mes.messageType == MessageType.C_INTERNAL
                        && mes.subType == (int)InternalDataType.I_ID_REQUEST)
                        if (enableAutoAssignId)
                            SendNewIdResponse();

                    return;
                }

                //Metric system request
                if (mes.messageType == MessageType.C_INTERNAL
                    && mes.subType == (int)InternalDataType.I_CONFIG)
                    SendMetricResponse(mes.nodeId);

                //Sensor request
                if (mes.messageType == MessageType.C_REQ)
                    ProceedRequestMessage(mes);


                UpdateNodeFromMessage(mes);
                UpdateSensorFromMessage(mes);
            }
        }

        private void ProceedRequestMessage(Message mes)
        {
            if (mes.messageType != MessageType.C_REQ)
                return;

            Node node = GetNode(mes.nodeId);
            if (node == null) return;

            Sensor sensor = node.GetSensor(mes.sensorId);
            if (sensor == null) return;

            SensorDataType dataType = (SensorDataType)mes.subType;
            SensorData data = sensor.GetData(dataType);
            if (data == null) return;

            SendSensorState(mes.nodeId, mes.sensorId, data);
        }


        private void UpdateNodeFromMessage(Message mes)
        {
            Node node = GetNode(mes.nodeId);

            if (node == null)
            {
                node = new Node(mes.nodeId);
                nodes.Add(node);

                if (OnNewNodeEvent != null)
                    OnNewNodeEvent(node);

                DebugGatewayState(String.Format("New node (id: {0}) registered", node.nodeId));
            }

            node.UpdateLastSeenNow();
            if (OnNodeLastSeenUpdatedEvent != null)
                OnNodeLastSeenUpdatedEvent(node);


            if (mes.sensorId == 255)
            {
                if (mes.messageType == MessageType.C_PRESENTATION)
                {
                    if (mes.subType == (int)SensorType.S_ARDUINO_NODE)
                    {
                        node.isRepeatingNode = false;
                    }
                    else if (mes.subType == (int)SensorType.S_ARDUINO_REPEATER_NODE)
                    {
                        node.isRepeatingNode = true;
                    }


                    if (OnNodeUpdatedEvent != null)
                        OnNodeUpdatedEvent(node);

                    DebugGatewayState(String.Format("Node {0} updated", node.nodeId));
                }
                else if (mes.messageType == MessageType.C_INTERNAL)
                {
                    if (mes.subType == (int)InternalDataType.I_SKETCH_NAME)
                    {
                        node.name = mes.payload;

                        if (OnNodeUpdatedEvent != null)
                            OnNodeUpdatedEvent(node);

                        DebugGatewayState(String.Format("Node {0} updated", node.nodeId));
                    }
                    else if (mes.subType == (int)InternalDataType.I_SKETCH_VERSION)
                    {
                        node.version = mes.payload;

                        if (OnNodeUpdatedEvent != null)
                            OnNodeUpdatedEvent(node);

                        DebugGatewayState(String.Format("Node {0} updated", node.nodeId));
                    }
                    else if (mes.subType == (int)InternalDataType.I_BATTERY_LEVEL)
                    {
                        node.batteryLevel = Int32.Parse(mes.payload);
                        if (OnNodeBatteryUpdatedEvent != null)
                            OnNodeBatteryUpdatedEvent(node);
                        return;
                    }
                }
            }

        }

        public void UpdateSensorFromMessage(Message mes)
        {
            //if internal node message
            if (mes.sensorId == 255)
                return;

            if (mes.messageType != MessageType.C_PRESENTATION
                && mes.messageType != MessageType.C_SET)
                return;

            Node node = GetNode(mes.nodeId);

            Sensor sensor = node.GetSensor(mes.sensorId);
            bool isNewSensor = false;

            if (sensor == null)
            {
                sensor = node.AddSensor(mes.sensorId);
                isNewSensor = true;
            }

            if (mes.messageType == MessageType.C_SET)
            {
                SensorDataType dataType = (SensorDataType)mes.subType;
                sensor.AddOrUpdateData(dataType, mes.payload);
            }
            else if (mes.messageType == MessageType.C_PRESENTATION)
            {
                if (mes.subType < 0 || mes.subType > (int)Enum.GetValues(typeof(SensorType)).Cast<SensorType>().Max())
                {
                    throw new ArgumentOutOfRangeException(
                        "This exception occurs when the serial port does not have time to write the data");
                }

                sensor.SetSensorType((SensorType)mes.subType);

                if (!String.IsNullOrEmpty(mes.payload))
                    sensor.description = mes.payload;
            }



            if (isNewSensor)
            {
                if (OnNewSensorEvent != null)
                    OnNewSensorEvent(sensor);

                DebugGatewayState(String.Format("New sensor (node id {0}, sensor id: {1}) registered", sensor.ownerNodeId,
                    sensor.sensorId));
            }
            else
            {
                if (OnSensorUpdatedEvent != null)
                    OnSensorUpdatedEvent(sensor);
            }

        }


        public Node GetNode(int id)
        {
            Node node = nodes.FirstOrDefault(x => x.nodeId == id);
            return node;
        }



        public Message ParseMessageFromString(string message)
        {
            var mes = new Message();

            try
            {
                string[] arguments = message.Split(new char[] { ';' }, 6);
                mes.nodeId = Int32.Parse(arguments[0]);
                mes.sensorId = Int32.Parse(arguments[1]);
                mes.messageType = (MessageType)Int32.Parse(arguments[2]);
                mes.ack = arguments[3] == "1";
                mes.subType = Int32.Parse(arguments[4]);
                mes.payload = arguments[5];
                mes.isValid = true;
            }
            catch
            {
                mes = new Message();
                mes.isValid = false;
                mes.payload = message;
            }
            return mes;
        }


        public List<Node> GetNodes()
        {
            return nodes;
        }

        public void AddNode(Node node)
        {
            nodes.Add(node);
        }


        public void SendSensorState(int nodeId, int sensorId, SensorData data)
        {
            Sensor sensor = GetNode(nodeId).GetSensor(sensorId);
            sensor.AddOrUpdateData(data);

            Message message = new Message();
            message.ack = false;
            message.messageType = MessageType.C_SET;
            message.nodeId = nodeId;
            message.payload = data.state;
            message.sensorId = sensorId;
            message.subType = (int)data.dataType;
            message.isValid = true;
            SendMessage(message);

            if (OnSensorUpdatedEvent != null)
                OnSensorUpdatedEvent(sensor);
        }

        public int GetFreeNodeId()
        {
            for (int i = 1; i < 254; i++)
            {
                bool found = false;

                foreach (var node in nodes)
                {
                    if (node.nodeId == i)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    return i;
                }
            }

            return 255;
        }

        public void SendNewIdResponse()
        {
            int freeId = GetFreeNodeId();

            Message mess = new Message();
            mess.nodeId = 255;
            mess.sensorId = 255;
            mess.messageType = MessageType.C_INTERNAL;
            mess.ack = false;
            mess.subType = (int)InternalDataType.I_ID_RESPONSE;
            mess.payload = freeId.ToString();
            mess.isValid = true;
            SendMessage(mess);
        }

        private void SendMetricResponse(int nodeId)
        {
            Message mess = new Message();
            mess.nodeId = nodeId;
            mess.sensorId = 255;
            mess.messageType = MessageType.C_INTERNAL;
            mess.ack = false;
            mess.subType = (int)InternalDataType.I_CONFIG;
            mess.payload = "M";
            mess.isValid = true;
            SendMessage(mess);
        }

        public void ClearNodesList()
        {
            nodes.Clear();

            if (OnClearNodesListEvent != null)
                OnClearNodesListEvent(this, null);
        }

        public async Task SendRebootToAllNodes()
        {
            for (int i = 1; i <= 254; i++)
            {
                SendReboot(i);
                await Task.Delay(10);
            }
        }

        public void SendReboot(int nodeId)
        {
            Message message = new Message();
            message.ack = false;
            message.messageType = MessageType.C_INTERNAL;
            message.nodeId = nodeId;
            message.payload = "0";
            message.sensorId = 0;
            message.subType = (int)InternalDataType.I_REBOOT;
            message.isValid = true;
            SendMessage(message);
        }

        public GatewayInfo GetGatewayInfo()
        {
            GatewayInfo info =new GatewayInfo();

            info.isGatewayConnected = IsConnected();

            info.gatewayNodesRegistered = nodes.Count;

            int sensors=0;
            foreach (var node in nodes)
                sensors += node.sensors.Count;
            info.gatewaySensorsRegistered = sensors;

            return info;
        }

        public void SetNodeDbId(int nodeId, int dbId)
        {
            Node node = GetNode(nodeId);
            node.db_Id = dbId;
        }

        public void SetSensorDbId(int ownerNodeId,int sensorId, int dbId)
        {
            Node node = GetNode(ownerNodeId);
            Sensor sensor = node.GetSensor(sensorId);
            sensor.db_Id = dbId;
        }
    }
}