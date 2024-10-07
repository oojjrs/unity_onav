using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace oojjrs.onav
{
    public class MyPath
    {
        public float Cost => FromNode.CostToTarget;
        public Vector3 Destination { get; }
        public MyFlowField Field { get; }
        public MyFlowField.NodeInterface FromNode { get; }
        public Vector3 LastDirection
        {
            get
            {
                if (FromNode.Target)
                {
                    return (Destination - Source).normalized;
                }
                else
                {
                    var lp = Positions.Last();
                    var llp = Positions.Reverse().Skip(1).First();
                    return (Destination - (lp - llp) / 2).normalized;
                }
            }
        }
        public IEnumerable<Vector3> Positions
        {
            get
            {
                var currentNode = FromNode;
                yield return currentNode.TileIntermediate.Position;

                while ((currentNode = currentNode.NextNode) != default)
                    yield return currentNode.TileIntermediate.Position;
            }
        }
        public Vector3 Source { get; }
        public MyFlowField.NodeInterface TargetNode
        {
            get
            {
                var currentNode = FromNode;
                while (currentNode != default && currentNode.Target == false)
                    currentNode = currentNode.NextNode;

                return currentNode;
            }
        }

        public MyPath(MyFlowField field, MyFlowField.NodeInterface fromNode, Vector3 src, Vector3 dst)
        {
            Debug.Assert(fromNode != default, "이거 왜 비었어?");

            Destination = dst;
            Field = field;
            FromNode = fromNode;
            Source = src;
        }

        public IEnumerable<Vector3> GetValidPositionOnPath(IEnumerable<(Vector3 position, Vector2Int coordinate)> tuples)
        {
            foreach (var tuple in tuples)
            {
                var node = Field.GetNode(tuple.coordinate);
                if (node != default)
                {
                    if (node.Reachable)
                        yield return tuple.position;
                }
            }
        }
    }
}
