using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace oojjrs.onav
{
    public class MyNavigator : MonoBehaviour
    {
        private Dictionary<Vector2Int, MyFlowField> Fields { get; } = new();
        public MyFlowField Latest { get; private set; }
        // TODO : 이 위치가 적합한가에 대해서는 생각의 여지가 있음.
        public MyRvoAgentContainer RvoAgentContainer { get; } = new();
        private Dictionary<Vector2Int, MyFlowField.TileIntermediate> Tiles { get; } = new();

        private event Func<Vector3, Vector2Int> PositionToCoordinate;
        public event Action<MyNavigator> OnUpdated;
        public event Action<MyNavigator, Vector3> OnUsed;

        public MyFlowField Calculate(Vector2Int to)
        {
            Debug.Assert(Tiles.Count > 0, "타일 정보가 없는데요? SetField부터 호출해주세요.");

            if (Fields.TryGetValue(to, out var field))
            {
                // TODO : 계산은 계산대로 하고, 리턴은 바로 할 수 있습니다..? 건설이 없으니까 가능하긴 한데 그게 좀 그렇지 않나.
                if (field.Calculating)
                    Latest = default;
                else
                    Latest = field;
            }
            else
            {
                field = new(Tiles.Values.ToArray(), to, PositionToCoordinate);
                Fields[to] = field;

                field.Calculate();
                Latest = field;
            }

            return Latest;
        }

        public void CalculateAsync(Vector2Int to, Action<MyNavigator, MyFlowField> onFinish, Func<bool> keepGoingOn = default)
        {
            Debug.Assert(Tiles.Count > 0, "타일 정보가 없는데요? SetField부터 호출해주세요.");

            if (Fields.TryGetValue(to, out var field))
            {
                // TODO : 계산은 계산대로 하고, 리턴은 바로 할 수 있습니다..? 건설이 없으니까 가능하긴 한데 그게 좀 그렇지 않나.
                if (field.Calculating)
                {
                    _ = StartCoroutine(Func());
                }
                else
                {
                    Latest = field;
                    onFinish?.Invoke(this, field);
                }

                IEnumerator Func()
                {
                    if (keepGoingOn == default)
                        keepGoingOn = () => true;

                    yield return new WaitUntil(() => field.Calculating == false || keepGoingOn() == false);

                    if (keepGoingOn())
                    {
                        Latest = field;
                        onFinish?.Invoke(this, field);
                    }
                }
            }
            else
            {
                field = new(Tiles.Values.ToArray(), to, PositionToCoordinate);
                Fields[to] = field;

                _ = StartCoroutine(field.CalculateAsync(() =>
                {
                    Latest = field;
                    onFinish?.Invoke(this, field);
                }, keepGoingOn));
            }
        }

        private MyFlowField.TileIntermediate GetClosestTile(Vector3 src, MyFlowField.TileIntermediate fromTile)
        {
            var useds = new HashSet<MyFlowField.TileIntermediate> { fromTile };
            var inspectingTiles = new List<MyFlowField.TileIntermediate> { fromTile };
            while (inspectingTiles.Count > 0)
            {
                // 최적화 때문에 except 안 쓴 거니까 바꾸지 마라.
                var neighborTiles = inspectingTiles.SelectMany(tile => tile.Neighbors.Select(coordinate => Tiles.TryGetValue(coordinate, out var tile) ? tile : default)).Distinct().Where(tile => useds.Contains(tile) == false).ToArray();
                inspectingTiles.Clear();

                var closestReachable = neighborTiles.Where(t => t.Tile.Walkable).OrderBy(tile => (tile.Position - src).sqrMagnitude).FirstOrDefault();
                if (closestReachable != default)
                {
                    return closestReachable;
                }
                else
                {
                    inspectingTiles.AddRange(neighborTiles);
                    foreach (var tile in neighborTiles)
                        useds.Add(tile);
                }
            }

            return default;
        }

        public bool IsWalkable(Vector2Int to)
        {
            if (Tiles.TryGetValue(to, out var tile))
                return tile.Tile.Walkable;
            else
                return false;
        }

        public MyPath Search(Vector3 src, Vector3 dst, Vector2Int from, Vector2Int to, bool strictFrom, bool strictTo)
        {
            Debug.Assert(Tiles.Count > 0, "타일 정보가 없는데요? SetField부터 호출해주세요.");

            if (Tiles.TryGetValue(from, out var fromTile) && Tiles.TryGetValue(to, out var toTile))
            {
                var fromOk = fromTile.Tile.Walkable;
                var toOk = toTile.Tile.Walkable;

                if (fromOk)
                {
                    if (toOk)
                    {
                        // 둘 다 유효한 칸이지만 서로 간에 길이 끊겨있을 수는 있어.
                        var ff = Calculate(to);
                        if (ff != default)
                        {
                            var path = ff.GetPath(from, src, dst);

                            OnUsed?.Invoke(this, path?.Destination ?? Vector3.zero);
                            return path;
                        }
                        else
                        {
                            return default;
                        }
                    }
                    // 도착지 근처를 찾아서 보내주는 것도 일인데...
                    else if (strictTo)
                    {
                        return default;
                    }
                    else
                    {
                        return SearchAroundTiles(src, dst, fromTile, toTile);
                    }
                }
                else if (strictFrom)
                {
                    return default;
                }
                // 출발지가 망가진 경우인데, 이 경우는 뭐 다른 곳으로 갈 수가 없고 그냥 출발지에서 긴급 탈출이 필요한 케이스다.
                else
                {
                    var closestTile = GetClosestTile(src, fromTile);
                    if (closestTile != default)
                    {
                        var ff = Calculate(closestTile.Coordinate);
                        if (ff != default)
                        {
                            var path = ff.GetPath(closestTile.Coordinate, src, closestTile.Position);

                            OnUsed?.Invoke(this, path?.Destination ?? Vector3.zero);
                            return path;
                        }
                        else
                        {
                            return default;
                        }
                    }
                    else
                    {
                        // 맵에 뭐 갈 수 있는 곳이 하나도 없다는데...
                        return default;
                    }
                }
            }
            // 너 뭐야 어떻게 이런 요청을 할 수 있었나
            else
            {
                return default;
            }
        }

        public void SearchAsync(Vector3 src, Vector3 dst, Vector2Int from, Vector2Int to, bool strictFrom, bool strictTo, Action<MyPath> onFinish, Func<bool> keepGoingOn = default)
        {
            Debug.Assert(Tiles.Count > 0, "타일 정보가 없는데요? SetField부터 호출해주세요.");

            if (Tiles.TryGetValue(from, out var fromTile) && Tiles.TryGetValue(to, out var toTile))
            {
                var fromOk = fromTile.Tile.Walkable;
                var toOk = toTile.Tile.Walkable;

                if (fromOk)
                {
                    if (toOk)
                    {
                        // 둘 다 유효한 칸이지만 서로 간에 길이 끊겨있을 수는 있어.
                        CalculateAsync(to, (nav, ff) =>
                        {
                            if (ff != default)
                            {
                                var path = ff.GetPath(from, src, dst);

                                // 동기 함수와 순서를 맞추기 위해 onFinish보다 먼저 호출하는 것으로 바꾸었다.
                                OnUsed?.Invoke(this, path?.Destination ?? Vector3.zero);

                                onFinish?.Invoke(path);
                            }
                            else
                            {
                                onFinish?.Invoke(default);
                            }
                        }, keepGoingOn);
                    }
                    // 도착지 근처를 찾아서 보내주는 것도 일인데...
                    else if (strictTo)
                    {
                        onFinish?.Invoke(default);
                    }
                    else
                    {
                        _ = StartCoroutine(SearchAroundTilesAsync(src, dst, fromTile, toTile, onFinish, keepGoingOn));
                    }
                }
                else if (strictFrom)
                {
                    onFinish?.Invoke(default);
                }
                // 출발지가 망가진 경우인데, 이 경우는 뭐 다른 곳으로 갈 수가 없고 그냥 출발지에서 긴급 탈출이 필요한 케이스다.
                else
                {
                    var closestTile = GetClosestTile(src, fromTile);
                    if (closestTile != default)
                    {
                        CalculateAsync(closestTile.Coordinate, (nav, ff) =>
                        {
                            if (ff != default)
                            {
                                var path = ff.GetPath(closestTile.Coordinate, src, closestTile.Position);

                                OnUsed?.Invoke(this, path?.Destination ?? Vector3.zero);

                                onFinish?.Invoke(path);
                            }
                            else
                            {
                                onFinish?.Invoke(default);
                            }
                        }, keepGoingOn);
                    }
                    else
                    {
                        // 맵에 뭐 갈 수 있는 곳이 하나도 없다는데...
                        onFinish?.Invoke(default);
                    }
                }
            }
            // 너 뭐야 어떻게 이런 요청을 할 수 있었나
            else
            {
                onFinish?.Invoke(default);
            }
        }

        private MyPath SearchAroundTiles(Vector3 src, Vector3 dst, MyFlowField.TileIntermediate fromTile, MyFlowField.TileIntermediate toTile)
        {
            var groups = toTile.Tile.AroundCoordinates.Select(c => Tiles.TryGetValue(c, out var tile) ? tile : default).Where(t => t != default && t.Tile.Walkable).GroupBy(t => t.Tile.GetCost(toTile.Tile)).OrderBy(g => g.Key);
            foreach (var group in groups)
            {
                foreach (var aroundTile in group.OrderBy(t => (t.Position - dst).sqrMagnitude + (t.Position - src).sqrMagnitude))
                {
                    var field = Calculate(aroundTile.Coordinate);
                    var path = field.GetPath(fromTile.Coordinate, src, aroundTile.Position + (toTile.Position - aroundTile.Position).normalized * aroundTile.Tile.Length);
                    if (path != default)
                    {
                        OnUsed?.Invoke(this, path.Destination);
                        return path;
                    }
                }
            }

            OnUsed?.Invoke(this, dst);
            return default;
        }

        private IEnumerator SearchAroundTilesAsync(Vector3 src, Vector3 dst, MyFlowField.TileIntermediate fromTile, MyFlowField.TileIntermediate toTile, Action<MyPath> onFinish, Func<bool> keepGoingOn = default)
        {
            var groups = toTile.Tile.AroundCoordinates.Select(c => Tiles.TryGetValue(c, out var tile) ? tile : default).Where(t => t != default && t.Tile.Walkable).GroupBy(t => t.Tile.GetCost(toTile.Tile)).OrderBy(g => g.Key);
            foreach (var group in groups)
            {
                foreach (var aroundTile in group.OrderBy(t => (t.Position - dst).sqrMagnitude + (t.Position - src).sqrMagnitude))
                {
                    var b = false;
                    var ret = false;
                    CalculateAsync(aroundTile.Coordinate, (nav, field) =>
                    {
                        var path = field.GetPath(fromTile.Coordinate, src, aroundTile.Position + (toTile.Position - aroundTile.Position).normalized * aroundTile.Tile.Length);
                        if (path != default)
                        {
                            b = true;

                            OnUsed?.Invoke(this, path.Destination);

                            onFinish?.Invoke(path);
                        }

                        ret = true;
                    }, keepGoingOn);

                    yield return new WaitUntil(() => ret);

                    if (b)
                        yield break;
                }
            }

            OnUsed?.Invoke(this, dst);

            onFinish?.Invoke(default);
        }

        public void SetField(MyFlowField.TileInterface[] tiles, Func<Vector3, Vector2Int> positionToCoordinate)
        {
            Fields.Clear();
            Tiles.Clear();

            foreach (var tile in tiles)
                Tiles.Add(tile.Coordinate, new(tile));

            PositionToCoordinate = positionToCoordinate;
        }

        public void UpdateFields(Action<MyNavigator, MyFlowField> onFinish, Func<bool> keepGoingOn = default)
        {
            Debug.Assert(Tiles.Count > 0, "타일 정보가 없는데요? SetField부터 호출해주세요.");

            foreach (var field in Fields.Values)
            {
                _ = StartCoroutine(field.RecalculateAsync(() =>
                {
                    onFinish?.Invoke(this, field);

                    if (Latest != default && Latest == field)
                        OnUpdated?.Invoke(this);
                }, keepGoingOn));
            }
        }
    }
}
