﻿using System.Collections.Generic;
using System.Linq;
using MyNetSensors.Gateways;
using MyNetSensors.Gateways.MySensors.Serial;
using Node = MyNetSensors.Nodes.Node;

namespace MyNetSensors.Nodes
{
    public class MySensorsNodesEngine
    {
        public static Gateway gateway;

        public static MySensorsNodesEngine mySensorsNodesEngine;
        private static NodesEngine engine;


        public MySensorsNodesEngine(Gateway gateway, NodesEngine engine)
        {
            MySensorsNodesEngine.gateway = gateway;
            MySensorsNodesEngine.mySensorsNodesEngine = this;
            MySensorsNodesEngine.engine = engine;

            gateway.OnNewNode += CreateOrUpdateNode;
            gateway.OnNodeUpdated += CreateOrUpdateNode;
            gateway.OnNewSensor += CreateOrUpdateSensor;
            gateway.OnSensorUpdated += CreateOrUpdateSensor;
            gateway.OnRemoveAllNodes += OnGatewayRemoveAllNodes;
            gateway.OnRemoveNode += OnGatewayRemoveNode;
            engine.OnRemoveAllNodesAndLinks += OnEngineRemoveAllNodesAndLinks;
            engine.OnRemoveNode += OnEngineRemoveNode;
            CreateAndAddMySensorsNodes();
        }

        private void OnEngineRemoveNode(Node node)
        {
            if (!(node is MySensorsNode))
                return;

            MySensorsNode n = (MySensorsNode) node;

            if (gateway.GetNode(n.nodeId)!=null)
                gateway.RemoveNode(n.nodeId);
        }

        private void OnEngineRemoveAllNodesAndLinks()
        {
            if (gateway.GetNodes().Any())
                gateway.RemoveAllNodes();
        }

        private void OnGatewayRemoveNode(Gateways.MySensors.Serial.Node node)
        {
            MySensorsNode oldNode = GetMySensorsNode(node.Id);

            if (oldNode != null)
                engine.RemoveNode(oldNode);
        }

        private void OnGatewayRemoveAllNodes()
        {
            List<MySensorsNode> nodes = engine.GetNodes().OfType<MySensorsNode>().ToList();
            foreach (var node in nodes)
            {
                engine.RemoveNode(node);
            }
        }


        private void CreateOrUpdateNode(Gateways.MySensors.Serial.Node node)
        {
            MySensorsNode oldNode = GetMySensorsNode(node.Id);
            if (oldNode == null)
            {
                MySensorsNode newMySensorsNode = new MySensorsNode(node);
                engine.AddNode(newMySensorsNode);
            }
            else
            {
                oldNode.Title = node.GetSimpleName2();
                engine.UpdateNode(oldNode, true);
            }
        }

        private void CreateOrUpdateSensor(Sensor sensor)
        {
            MySensorsNodeOutput output = GetMySensorsNodeOutput(sensor);
            if (output == null)
            {
                MySensorsNode node = GetMySensorsNode(sensor.nodeId);
                node.AddInputAndOutput(sensor);
                engine.UpdateNode(node, true);
            }
            else
            {
                engine.UpdateOutput(output.Id, sensor.state, sensor.GetSimpleName3());
            }

        }




        public MySensorsNode GetMySensorsNode(int nodeId)
        {
            return engine.GetNodes()
                .OfType<MySensorsNode>()
                .FirstOrDefault(node => node.nodeId == nodeId);
        }

        public MySensorsNodeOutput GetMySensorsNodeOutput(int nodeId, int sensorId)
        {
            MySensorsNode oldNode = GetMySensorsNode(nodeId);
            if (oldNode == null)
                return null;

            return oldNode.Outputs
                .Cast<MySensorsNodeOutput>()
                .FirstOrDefault(output => output.sensorId == sensorId);
        }



        public MySensorsNodeInput GetMySensorsNodeInput(int nodeId, int sensorId)
        {
            MySensorsNode oldNode = GetMySensorsNode(nodeId);
            if (oldNode == null)
                return null;

            return oldNode.Inputs
                .Cast<MySensorsNodeInput>()
                .FirstOrDefault(input => input.sensorId == sensorId);
        }

        public MySensorsNodeOutput GetMySensorsNodeOutput(Sensor sensor)
        {
            return GetMySensorsNodeOutput(sensor.nodeId, sensor.sensorId);
        }



        public MySensorsNodeInput GetMySensorsNodeInput(Sensor sensor)
        {
            return GetMySensorsNodeInput(sensor.nodeId, sensor.sensorId);
        }

        public List<MySensorsNode> CreateAndAddMySensorsNodes()
        {
            var list = new List<MySensorsNode>();

            foreach (var node in gateway.GetNodes())
            {
                if (GetMySensorsNode(node.Id) != null)
                    continue;

                MySensorsNode newMySensorsNode = new MySensorsNode(node);
                list.Add(newMySensorsNode);
                engine.AddNode(newMySensorsNode);
            }
            return list;
        }

    }
}