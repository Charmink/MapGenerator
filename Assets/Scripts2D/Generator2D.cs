using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;
using Graphs;

public class Generator2D : MonoBehaviour {
    enum CellType {
        None,
        Room,
        Hallway
    }

    class Room {
        public RectInt bounds;

        public Room(Vector2Int location, Vector2Int size) {
            bounds = new RectInt(location, size);
        }

        public static bool Intersect(Room a, Room b) {
            return !((a.bounds.position.x >= (b.bounds.position.x + b.bounds.size.x)) || ((a.bounds.position.x + a.bounds.size.x) <= b.bounds.position.x)
                || (a.bounds.position.y >= (b.bounds.position.y + b.bounds.size.y)) || ((a.bounds.position.y + a.bounds.size.y) <= b.bounds.position.y));
        }
    }

    [SerializeField]
    Vector2Int size;
    [SerializeField]
    int roomCount;
    [SerializeField]
    Vector2Int roomMaxSize;
    [SerializeField]
    Vector2Int roomMinSize;
    [SerializeField]
    GameObject cubePrefab;
    [SerializeField]
    Material redMaterial;
    [SerializeField]
    Material blueMaterial;

    Random random;
    Grid2D<CellType> grid;
    List<Room> rooms;
    Delaunay2D delaunay;
    HashSet<Prim.Edge> selectedEdges;
    void Start() {
        Generate();
    }

    void Generate() {
        random = new Random(0);
        grid = new Grid2D<CellType>(size, Vector2Int.zero);
        rooms = new List<Room>();

        PlaceRooms();
        Triangulate();
        //PlaceEdges();
        CreateHallways();
            // PlaceSelectedEdges();
        PathfindHallways();
    }

    void PlaceEdgesS()
    {
        for (int i = 0; i < delaunay.Edges.Count; i++)
        {
            GameObject lineObject = new GameObject("Line" + i); // Создаем объект для линии
            LineRenderer line = lineObject.AddComponent<LineRenderer>(); // Добавляем компонент LineRenderer
            line.positionCount = 2;
            line.SetPosition(0,new Vector3(delaunay.Edges[i].U.Position.x, 0, delaunay.Edges[i].U.Position.y));
            line.SetPosition(1, new Vector3(delaunay.Edges[i].V.Position.x, 0, delaunay.Edges[i].V.Position.y));
            line.material = blueMaterial;
        }
    }
    
    void PlaceEdges()
    {
        for (int i = 0; i < delaunay.Edges.Count; i++)
        {
            Debug.DrawLine( new Vector3(delaunay.Edges[i].U.Position.x,  delaunay.Edges[i].U.Position.z, delaunay.Edges[i].U.Position.y), new Vector3(delaunay.Edges[i].V.Position.x, delaunay.Edges[i].V.Position.z, delaunay.Edges[i].V.Position.y), Color.blue, 99999, false);
            /*GameObject lineObject = new GameObject("Line" + i); // Создаем объект для линии
            LineRenderer line = lineObject.AddComponent<LineRenderer>(); // Добавляем компонент LineRenderer
            line.positionCount = 2;
            line.SetPosition(0,  new Vector3(delaunay.Edges[i].U.Position.x, delaunay.Edges[i].U.Position.y, delaunay.Edges[i].U.Position.z));
            line.SetPosition(1, new Vector3(delaunay.Edges[i].V.Position.x, delaunay.Edges[i].V.Position.y, delaunay.Edges[i].V.Position.z));
            line.material = blueMaterial;*/
        }
    }
    
    void PlaceSelectedEdges()
    {
        var edgesEnumerator = selectedEdges.GetEnumerator();
        while (edgesEnumerator.MoveNext())
        {
            var currentEdge = edgesEnumerator.Current;
            Debug.DrawLine( new Vector3(currentEdge.U.Position.x,  currentEdge.U.Position.z, currentEdge.U.Position.y), new Vector3(currentEdge.V.Position.x, currentEdge.V.Position.z, currentEdge.V.Position.y), Color.blue, 99999, false);
        }
      
    }

    void PlaceRooms() {
        for (int i = 0; i < roomCount; i++) {
            Vector2Int location = new Vector2Int(
                random.Next(0, size.x),
                random.Next(0, size.y)
            );

            Vector2Int roomSize = new Vector2Int(
                random.Next(roomMinSize.x, roomMaxSize.x + 1),
                random.Next(roomMinSize.y, roomMaxSize.y + 1)
            );

            bool add = true;
            Room newRoom = new Room(location, roomSize);
            Room buffer = new Room(location + new Vector2Int(-2, -2), roomSize + new Vector2Int(3, 3));

            foreach (var room in rooms) {
                if (Room.Intersect(room, buffer)) {
                    add = false;
                    i--;
                    break;
                }
            }

            if (newRoom.bounds.xMin < 0 || newRoom.bounds.xMax >= size.x
                || newRoom.bounds.yMin < 0 || newRoom.bounds.yMax >= size.y) {
                add = false;
                i--;
            }

            if (add) {
                rooms.Add(newRoom);
                PlaceRoom(newRoom.bounds.position, newRoom.bounds.size);

                foreach (var pos in newRoom.bounds.allPositionsWithin) {
                    grid[pos] = CellType.Room;
                }
            }
        }
    }

    void Triangulate() {
        List<Vertex> vertices = new List<Vertex>();

        foreach (var room in rooms) {
            vertices.Add(new Vertex<Room>((Vector2)room.bounds.position + ((Vector2)room.bounds.size) / 2, room));
        }

        delaunay = Delaunay2D.Triangulate(vertices);
    }

    void CreateHallways() {
        List<Prim.Edge> edges = new List<Prim.Edge>();

        foreach (var edge in delaunay.Edges) {
            edges.Add(new Prim.Edge(edge.U, edge.V));
        }

        List<Prim.Edge> mst = Prim.MinimumSpanningTree(edges, edges[0].U);

        selectedEdges = new HashSet<Prim.Edge>(mst);
        var remainingEdges = new HashSet<Prim.Edge>(edges);
        remainingEdges.ExceptWith(selectedEdges);

        foreach (var edge in remainingEdges) {
            if (random.NextDouble() < 0.125) {
                selectedEdges.Add(edge);
            }
        }
    }

    void PathfindHallways() {
        DungeonPathfinder2D aStar = new DungeonPathfinder2D(size);

        foreach (var edge in selectedEdges) {
            var startRoom = (edge.U as Vertex<Room>).Item;
            var endRoom = (edge.V as Vertex<Room>).Item;
            var startPosf = startRoom.bounds.position;
            var endPosf = endRoom.bounds.position;
            var startPos = new Vector2Int((int)startPosf.x, (int)startPosf.y);
            var endPos = new Vector2Int((int)endPosf.x, (int)endPosf.y);

            var path = aStar.FindPath(startPos, endPos, (DungeonPathfinder2D.Node a, DungeonPathfinder2D.Node b) => {
                var pathCost = new DungeonPathfinder2D.PathCost();
                
                pathCost.cost = Vector2Int.Distance(b.Position, endPos);    //heuristic

                if (grid[b.Position] == CellType.Room) {
                    pathCost.cost += 10;
                } else if (grid[b.Position] == CellType.None) {
                    pathCost.cost += 1;
                } else if (grid[b.Position] == CellType.Hallway) {
                    pathCost.cost += 15;
                }

                pathCost.traversable = true;

                return pathCost;
            });

            if (path != null) {
                for (int i = 0; i < path.Count; i++) {
                    var current = path[i];
                    grid[current] = CellType.Hallway;
                }

                foreach (var pos in path) {
                    if (grid[pos] == CellType.Hallway) {
                        PlaceHallway(pos);
                    }
                }
            }
        }
    }

    void PlaceCube(Vector2Int location, Vector2Int size, Material material) {
        GameObject go = Instantiate(cubePrefab, new Vector3(location.x, 0, location.y), Quaternion.identity);
        go.GetComponent<Transform>().localScale = new Vector3(size.x, 1, size.y);
        go.GetComponent<MeshRenderer>().material = material;
    }

    void PlaceRoom(Vector2Int location, Vector2Int size) {
        PlaceCube(location, size, redMaterial);
    }

    void PlaceHallway(Vector2Int location) {
        PlaceCube(location, new Vector2Int(1, 1), blueMaterial);
    }
}
