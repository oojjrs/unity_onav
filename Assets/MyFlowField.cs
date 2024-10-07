using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace oojjrs.onav
{
    public class MyFlowField
    {
        public interface NodeInterface
        {
            float CostToTarget { get; }
            NodeInterface NextNode { get; }
            Vector3? Power { get; }
            bool Reachable { get; }
            bool Target { get; }
            TileIntermediate TileIntermediate { get; }
        }

        public interface TileInterface
        {
            IEnumerable<Vector2Int> AroundCoordinates { get; }
            Vector2Int Coordinate { get; }
            // 타일 중심에서 가장자리로 이동할 때 사용한다. 최대 길이이기 때문에 사각형, 육각형에 따라 타일을 벗어나지 않게 적절한 값을 정하도록
            float Length { get; }
            IEnumerable<Vector2Int> Neighbors { get; }
            Vector3 Position { get; }
            bool Walkable { get; }

            float GetCost(TileInterface toTile);
            bool IsIn(Vector3 pos);
        }

        private class Node : NodeInterface
        {
            public float CostToTarget { get; set; }
            public bool Fixed { get; set; }
            public NodeInterface NextNode { get; set; }
            public Vector3? Power { get; set; }
            // 헷갈리지 않도록 일부러 속성명을 다르게 만들었다.
            public bool Reachable => Power.HasValue || Target;
            public bool Target { get; }
            public TileIntermediate TileIntermediate { get; }

            public Node(TileIntermediate tile, bool target)
            {
                Fixed = target;
                Target = target;
                TileIntermediate = tile;
            }
        }

        public class TileIntermediate
        {
            public Vector2Int Coordinate { get; }
            public Vector2Int[] Neighbors { get; }
            public Vector3 Position { get; }
            public TileInterface Tile { get; }

            public TileIntermediate(TileInterface tile)
            {
                Coordinate = tile.Coordinate;
                Neighbors = tile.Neighbors.ToArray();
                Position = tile.Position;
                Tile = tile;
            }
        }

        public IEnumerable<NodeInterface> AllNodes => Nodes.Values;
        public bool Calculating { get; private set; }
        private Dictionary<Vector2Int, Node> Nodes { get; }
        private Node TargetNode { get; }

        private event Func<Vector3, Vector2Int> PositionToCoordinate;

        public MyFlowField(TileIntermediate[] tiles, Vector2Int target, Func<Vector3, Vector2Int> positionToCoordinate)
        {
            Nodes = tiles.ToDictionary(tile => tile.Coordinate, tile => new Node(tile, tile.Coordinate == target));
            TargetNode = Nodes[target];

            PositionToCoordinate = positionToCoordinate;
        }

        public void Calculate(Func<bool> keepGoingOn = default)
        {
            if (keepGoingOn == default)
                keepGoingOn = () => true;

            var q = new Queue<Node>();
            q.Enqueue(TargetNode);

            if (TargetNode.TileIntermediate.Tile.Walkable)
            {
                while (keepGoingOn() && q.Count > 0)
                {
                    var node = q.Dequeue();
                    var ret = GetNeighbors(node).Where(t => t.Fixed == false);
                    if (ret.Any())
                    {
                        var neighbors = ret.ToArray();
                        foreach (var neighborNode in neighbors)
                        {
                            if (neighborNode.TileIntermediate.Tile.Walkable)
                            {
                                var lowestCostNode = GetFixeds(neighborNode).OrderBy(t => t.CostToTarget).First();
                                neighborNode.CostToTarget = lowestCostNode.CostToTarget + lowestCostNode.TileIntermediate.Tile.GetCost(neighborNode.TileIntermediate.Tile);
                                neighborNode.NextNode = lowestCostNode;
                                neighborNode.Power = lowestCostNode.TileIntermediate.Position - neighborNode.TileIntermediate.Position;

                                // walkable의 친구들만 검사하는 게 맞지.
                                q.Enqueue(neighborNode);
                            }
                        }

                        foreach (var nnode in neighbors)
                            nnode.Fixed = true;
                    }
                }
            }
        }

        public IEnumerator CalculateAsync(Action onFinish, Func<bool> keepGoingOn)
        {
            Calculating = true;

            ThreadPool.QueueUserWorkItem(args =>
            {
                if (keepGoingOn == default)
                    keepGoingOn = () => true;

                Calculate(keepGoingOn);

                Calculating = false;
            });

            yield return new WaitUntil(() => Calculating == false);

            if (keepGoingOn())
                onFinish?.Invoke();
        }

        public bool CanMove(Vector2Int c)
        {
            var node = GetNode(c);
            return node != default && node.TileIntermediate.Tile.Walkable;
        }

        private IEnumerable<Node> GetFixeds(Node node)
        {
            foreach (var coordinate in node.TileIntermediate.Neighbors)
            {
                if (Nodes.TryGetValue(coordinate, out var value) && value.Fixed && value.TileIntermediate.Tile.Walkable)
                    yield return value;
            }
        }

        private IEnumerable<Node> GetNeighbors(Node node)
        {
            foreach (var coordinate in node.TileIntermediate.Neighbors)
            {
                if (Nodes.TryGetValue(coordinate, out var value))
                    yield return value;
            }
        }

        public NodeInterface GetNode(Vector2Int from)
        {
            Nodes.TryGetValue(from, out var node);
            return node;
        }

        public NodeInterface GetNodeByPosition(Vector3 position)
        {
            return GetNode(PositionToCoordinate(position));
        }

        public MyPath GetPath(Vector2Int from, Vector3 src, Vector3 dst)
        {
            if (Nodes.TryGetValue(from, out var fromNode))
            {
                if (fromNode.Target || fromNode.NextNode != default)
                    return new(this, fromNode, src, dst);
                else
                    return default;
            }
            else
            {
                return default;
            }
        }

        public IEnumerator RecalculateAsync(Action onFinish, Func<bool> keepGoingOn)
        {
            foreach (var node in Nodes.Values)
                node.Fixed = node.Target;

            return CalculateAsync(onFinish, keepGoingOn);
        }
    }
}
