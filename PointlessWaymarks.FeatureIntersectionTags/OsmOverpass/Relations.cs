//using NetTopologySuite.Geometries;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace PointlessWaymarks.FeatureIntersectionTags.OsmOverpass
//{
//    internal class Relations
//    {

//        private bool IsMultipolygon(OsmElement relation)
//        {
//            if (relation.Tags.ContainsKey(TYPE) == false) return false;
//            return relation.Tags[TYPE] == MULTIPOLYGON || relation.Tags[TYPE] == BOUNDARY;
//        }

//        private MultiPolygon MergeInnerIntoOuterPolygon(List<Polygon> outerPolygons, List<Polygon> innerPolygons)
//        {
//            var newOuterPolygons = new List<Polygon>();
//            outerPolygons = MergePolygons(outerPolygons);
//            foreach (var outerPolygon in outerPolygons)
//            {
//                // remove all inner holes from outer polygon
//                var newOuterPolygon = _geometryFactory.CreatePolygon((LinearRing)outerPolygon.ExteriorRing.Copy());
//                // get inner polygons
//                var currentInnerPolygons = innerPolygons.Where(p => p.Within(newOuterPolygon)).ToArray();
//                if (!currentInnerPolygons.Any())
//                {
//                    newOuterPolygons.Add(newOuterPolygon);
//                    continue;
//                }

//                var holesPolygons = currentInnerPolygons
//                    .Select(p => _geometryFactory.CreatePolygon(p.ExteriorRing.Copy() as LinearRing)).ToArray();
//                var holesUnifiedGeometry = CascadedPolygonUnion.Union(holesPolygons);
//                // adding the difference between the outer polygon and all the holes inside it
//                if (newOuterPolygon.Difference(holesUnifiedGeometry) is Polygon difference)
//                    newOuterPolygons.Add(difference);
//                // update list for next loop cycle
//                innerPolygons = innerPolygons.Except(currentInnerPolygons).ToList();
//            }

//            return _geometryFactory.CreateMultiPolygon(newOuterPolygons.Union(innerPolygons).ToArray());
//        }

//        private List<Polygon> MergePolygons(List<Polygon> polygons)
//        {
//            if (!polygons.Any()) return polygons;
//            try
//            {
//                var merged = CascadedPolygonUnion.Union(polygons.ToArray());
//                if (merged is MultiPolygon multipolygon) return multipolygon.Geometries.Cast<Polygon>().ToList();

//                var returnPolygon = merged as Polygon;
//                if (returnPolygon is null) return [];
//                return [returnPolygon];
//            }
//            catch
//            {
//                return polygons;
//            }
//        }

//        /// <summary>
//        ///     The purpose of this method is to take grouped results that grouped into "O" shape and lines that
//        ///     touches this "O" shape and turn them into a "Q" shape.
//        ///     This should only be applied to multiline strings
//        ///     It does so by going over all the circles, finding lines that are not circles that touches those
//        ///     and reorder the points, adding a new line and removes the circle and the line from the original list
//        /// </summary>
//        /// <param name="nodeGroups">The original list of list of nodes to alter</param>
//        /// <returns>A new list of list of nodes after the changes</returns>
//        private List<List<OsmNode>> RearrangeInCaseOfCircleAndLine(List<List<OsmNode>> nodeGroups)
//        {
//            if (nodeGroups.Count == 1) return nodeGroups;
//            var circles = nodeGroups.Where(g => g.First().Id == g.Last().Id).ToList();
//            if (!circles.Any()) return nodeGroups;
//            foreach (var circle in circles)
//            {
//                var lineThatTouchesTheCircle = nodeGroups
//                    .Except(circles).FirstOrDefault(g => circle
//                        .Any(n => n.Id == g.First().Id || n.Id == g.Last().Id));
//                if (lineThatTouchesTheCircle == null) continue;
//                nodeGroups.Remove(circle);
//                nodeGroups.Remove(lineThatTouchesTheCircle);
//                var nodeInCircleThatTouches = circle.FirstOrDefault(n => n.Id == lineThatTouchesTheCircle.Last().Id);
//                if (nodeInCircleThatTouches != null)
//                {
//                    var indexInCircle = circle.IndexOf(nodeInCircleThatTouches);
//                    var newList = lineThatTouchesTheCircle;
//                    newList.AddRange(circle.Skip(indexInCircle + 1).ToList());
//                    newList.AddRange(circle.Skip(1).Take(indexInCircle));
//                    nodeGroups.Add(newList);
//                    continue;
//                }

//                nodeInCircleThatTouches = circle.FirstOrDefault(n => n.Id == lineThatTouchesTheCircle.First().Id);
//                if (nodeInCircleThatTouches != null)
//                {
//                    var indexInCircle = circle.IndexOf(nodeInCircleThatTouches);
//                    var newList = circle.Skip(1).Take(indexInCircle - 1).ToList();
//                    newList.AddRange(lineThatTouchesTheCircle);
//                    newList.InsertRange(0, circle.Skip(indexInCircle));
//                    nodeGroups.Add(newList);
//                }
//            }

//            return nodeGroups;
//        }

//        /// <summary>
//        ///     This split by loop algorithm looks for duplicate ids inside a list of nodes,
//        ///     removes the shortest list between two duplicate ids and recursively adds these loops to a list
//        ///     The reasoning behind this algorithm is that when converting a list of nodes to polygons you need
//        ///     to split the different polygons to avoid creating invalid polygon that intersect itself
//        /// </summary>
//        /// <param name="nodes"></param>
//        /// <returns>A list of list with valid polygons or lines</returns>
//        private static List<List<OsmNode>> SplitListByLoops(List<OsmNode> nodes)
//        {
//            var groups = nodes.GroupBy(n => n.Id).ToList();
//            var isSimplePolygon = nodes.First().Id == nodes.Last().Id &&
//                                  groups.Count(g => g.Count() == 2) == 1 &&
//                                  groups.Count(g => g.Count() > 2) == 0;
//            if (groups.All(g => g.Count() == 1) || isSimplePolygon) return [nodes];
//            var duplicateIdentifiers = groups.Where(g => g.Count() > 1).Select(g => g.First().Id);
//            var minimalIndexStart = -1;
//            var minimalIndexEnd = -1;
//            // find the shortest loop:
//            foreach (var duplicateIdentifier in duplicateIdentifiers)
//            {
//                var firstIndex = -1;
//                var lastIndex = -1;
//                for (var nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
//                    if (nodes[nodeIndex].Id == duplicateIdentifier)
//                    {
//                        if (firstIndex == -1)
//                            firstIndex = nodeIndex;
//                        else
//                            lastIndex = nodeIndex;
//                        if (lastIndex == -1 || firstIndex == -1) continue;
//                        if (minimalIndexStart == -1 || lastIndex - firstIndex < minimalIndexEnd - minimalIndexStart)
//                        {
//                            minimalIndexStart = firstIndex;
//                            minimalIndexEnd = lastIndex;
//                        }
//                    }
//            }

//            // remove the loop:
//            var list = new List<List<OsmNode>>();
//            var loop = nodes.Skip(minimalIndexStart).Take(minimalIndexEnd - minimalIndexStart + 1).ToList();
//            list.Add(loop);
//            var leftOsmNodes = nodes.Take(minimalIndexStart).Concat(nodes.Skip(minimalIndexEnd)).ToList();
//            // run this again on the nodes without the above loop
//            return list.Concat(SplitListByLoops(leftOsmNodes)).ToList();
//        }


//        //private Feature ConvertToMultipolygon(OsmRelation relation)
//        //{
//        //    var allOsmWaysInOsmRelationByRole = GetAllOsmWaysGroupedByRole(relation);
//        //    var outerOsmWays = allOsmWaysInOsmRelationByRole.Where(kvp => kvp.Key == OUTER).SelectMany(kvp => kvp.Value).ToList();
//        //    var outerPolygons = GetGeometriesFromOsmWays(outerOsmWays, true).OfType<Polygon>().ToList();
//        //    var innerOsmWays = allOsmWaysInOsmRelationByRole.Where(kvp => kvp.Key == INNER).SelectMany(kvp => kvp.Value).ToList();
//        //    var innerPolygons = GetGeometriesFromOsmWays(innerOsmWays, true).OfType<Polygon>().ToList();
//        //    var multiPolygon = MergeInnerIntoOuterPolygon(outerPolygons, innerPolygons);
//        //    return new Feature(multiPolygon, ConvertOsmTagsToAttributesTable(relation));
//        //}

//        ///// <summary>
//        /////     A static method that gets all the ways from a relation recursively
//        ///// </summary>
//        ///// <param name="relation"></param>
//        ///// <returns></returns>
//        //public static List<OsmWay> GetAllOsmWays(OsmRelation relation)
//        //{
//        //    return GetAllOsmWaysGroupedByRole(relation).SelectMany(kvp => kvp.Value).ToList();
//        //}

//        //private static Dictionary<string, List<OsmWay>> GetAllOsmWaysGroupedByRole(OsmRelation relation)
//        //{
//        //    var dictionary = relation.Members.GroupBy(m => m.Role ?? string.Empty)
//        //        .ToDictionary(g => g.Key, g => g.Select(k => k.Member)
//        //            .OfType<OsmWay>().ToList());
//        //    if (relation.Members.All(m => m.Member.Type != OsmGeoType.OsmRelation)) return dictionary;
//        //    var subOsmRelations = relation.Members.Where(m => m.Role != SUBAREA).Select(m => m.Member)
//        //        .OfType<OsmRelation>();
//        //    foreach (var subOsmRelation in subOsmRelations)
//        //    {
//        //        var subOsmRelationDictionary = GetAllOsmWaysGroupedByRole(subOsmRelation);
//        //        foreach (var key in subOsmRelationDictionary.Keys)
//        //            if (dictionary.ContainsKey(key))
//        //                dictionary[key].AddRange(subOsmRelationDictionary[key]);
//        //            else
//        //                dictionary[key] = subOsmRelationDictionary[key];
//        //    }

//        //    return dictionary;
//        //}

//        //private Feature? ConvertOsmRelation(OsmRelation relation)
//        //{
//        //    if (IsMultipolygon(relation)) return ConvertToMultipolygon(relation);

//        //    var nodes = relation.Members.Select(m => m.Member).OfType<OsmNode>().ToList();
//        //    if (nodes.Any() && nodes.Count == relation.Members.Length)
//        //    {
//        //        var multiPoint =
//        //            _geometryFactory.CreateMultiPoint(nodes.Select(n => _geometryFactory.CreatePoint(OsmNodeToCoordinate(n)))
//        //                .ToArray());
//        //        return new Feature(multiPoint, ConvertOsmTagsToAttributesTable(relation));
//        //    }

//        //    var geometries = GetGeometriesFromOsmWays(GetAllOsmWays(relation), false);
//        //    if (!geometries.Any()) return null;
//        //    var multiLineString = _geometryFactory.CreateMultiLineString(geometries.Cast<LineString>().ToArray());
//        //    return new Feature(multiLineString, ConvertOsmTagsToAttributesTable(relation));
//        //}


//        private List<Geometry> GetGeometriesFromOsmWays(IEnumerable<OsmWayWithGeometry> ways, bool closePolygons)
//        {
//            var nodesGroups = new List<List<OsmNode>>();
//            var waysToGroup = new List<OsmWayWithGeometry>(ways.Where(w => w.GeometryNodes.Any()));
//            while (waysToGroup.Any())
//            {
//                var wayToGroup = waysToGroup.FirstOrDefault(w =>
//                    nodesGroups.Any(g => CanBeMerged(w.GeometryNodes, g)));

//                if (wayToGroup == null)
//                {
//                    nodesGroups.Add([.. waysToGroup.First().GeometryNodes]);
//                    waysToGroup.RemoveAt(0);
//                    continue;
//                }

//                var currentOsmNodes = wayToGroup.GeometryNodes.ToList();
//                waysToGroup.Remove(wayToGroup);
//                var group = nodesGroups.First(g => CanBeMerged(currentOsmNodes, g));
//                if (CanBeReverseMerged(group, currentOsmNodes))
//                {
//                    if (wayToGroup.Tags != null &&
//                        ((wayToGroup.Tags.ContainsKey("oneway") && wayToGroup.Tags["oneway"] == "yes") ||
//                         (wayToGroup.Tags.ContainsKey("oneway:mtb") && wayToGroup.Tags["oneway:mtb"] == "yes")))
//                        group.Reverse();
//                    else
//                        currentOsmNodes.Reverse(); // direction of this way is incompatible with other ways.
//                }

//                if (currentOsmNodes.First().Id == group.Last().Id)
//                {
//                    currentOsmNodes.RemoveAt(0);
//                    group.AddRange(currentOsmNodes);
//                    continue;
//                }

//                currentOsmNodes.RemoveAt(currentOsmNodes.Count -
//                                      1); // must use indexes since the same reference can be used at the start and end
//                group.InsertRange(0, currentOsmNodes);
//            }

//            var nodes = closePolygons
//                ? nodesGroups.Select(SplitListByLoops).SelectMany(g => g).ToList()
//                : RearrangeInCaseOfCircleAndLine(nodesGroups);

//            return nodes.Select(g => GetGeometryFromOsmNodes(g.ToArray(), closePolygons)).ToList();
//        }

//        private readonly GeometryFactory _geometryFactory;

//        private bool CanBeMerged(List<OsmNode> nodes1, List<OsmNode> nodes2)
//        {
//            return nodes1.Last().Id == nodes2.First().Id ||
//                   nodes1.First().Id == nodes2.Last().Id ||
//                   CanBeReverseMerged(nodes1, nodes2);
//        }

//        private bool CanBeReverseMerged(List<OsmNode> nodes1, List<OsmNode> nodes2)
//        {
//            return nodes1.First().Id == nodes2.First().Id ||
//                   nodes1.Last().Id == nodes2.Last().Id;
//        }

//        private const string BOUNDARY = "boundary";
//        private const string INNER = "inner";
//        private const string MULTIPOLYGON = "multipolygon";
//        private const string OUTER = "outer";
//        private const string SUBAREA = "subarea";
//        private const string TYPE = "type";

//    }
//}

