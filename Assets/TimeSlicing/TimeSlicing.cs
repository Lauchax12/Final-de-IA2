using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

public class TimeSlicing 
{
    #region Clase

    //Generator "generico" con un acum.Algo similar a la idea de Aggregate,
    //pero sin estar recorriendo un IEnumerable
    public static IEnumerable<T> Generate<T>(T seed, Func<T, T> modify)
    {
        T acum = seed;
        while (true)
        {
            yield return acum;
            acum = modify(acum);
        }

    }

    public static IEnumerable<T> CreatorObjects<T>(int count, Func<T> spawnMethod)
    {
        IEnumerable<T> collection = Enumerable.Empty<T>();
        while (count > 0)
        {
            count--;
            IEnumerable<T> temp = Enumerable.Empty<T>();
            collection = collection.Concat(temp.DefaultIfEmpty(spawnMethod()));
        }
        return collection;
    }

    //Ejemplo de funcion con time slicing para spawnear objetos cada x tiempo
    public static IEnumerator CreatorObjects<T>(int count, float time, Func<T> spawnMethod, Action<IEnumerable<T>> callback)
    {
        IEnumerable<T> collection = Enumerable.Empty<T>();
        WaitForSeconds timer = new WaitForSeconds(time);
        while (count > 0)
        {
            count--;
            IEnumerable<T> temp = Enumerable.Empty<T>();
            collection = collection.Concat(temp.DefaultIfEmpty(spawnMethod()));
            yield return timer;
        }
        callback(collection);
    }


    public static IEnumerable<T> CreatorObjectsGenerator<T>(int count, Func<T> fabricator)
    {
        while (count > 0)
        {
            count--;
            yield return fabricator();
        }
    }


    public static IEnumerator CreatorObjectsV2<T>(int count, float time, Func<T> spawnMethod,Action<T> elementCallback, Action<IEnumerable<T>> finalCallback)
    {
        IEnumerable<T> collection = Enumerable.Empty<T>();
        foreach (var item in CreatorObjectsGenerator(count,spawnMethod))
        {
            collection = collection.Concat(Enumerable.Empty<T>().DefaultIfEmpty(item));
            yield return new WaitForSeconds(time);
        }
        finalCallback(collection);
    }










    //Dijkstra con Linq y pausado
    static IEnumerable<Tuple<T, IEnumerable<T>>> Dijkstra<T>(T start,
                                                           Func<T, bool> targetCheck,
                                                           Func<T, IEnumerable<Tuple<T, float>>> GetNeighbours)
    {
        HashSet<T> visited = new HashSet<T>();
        Dictionary<T, T> previous = new Dictionary<T, T>();
        Dictionary<T, float> distances = new Dictionary<T, float>();
        List<T> pending = new List<T>();

        distances.Add(start, 0);
        pending.Add(start);

        while (pending.Any())
        {
            T current = pending.OrderBy(x => distances[x]).First();
            pending.Remove(current);
            visited.Add(current);

            if (targetCheck(current))
            {
                var path = Generate(current, x => previous[x])
                        .TakeWhile(x => previous.ContainsKey(x))
                        .Reverse();

                yield return Tuple.Create(current, path);
                break;
            }
            else
            {
                var n = GetNeighbours(current).Where(x => !visited.Contains(x.Item1));
                foreach (var elem in n)
                {
                    var altDist = distances[current] + elem.Item2;
                    if (!distances.ContainsKey(elem.Item1) || distances[elem.Item1] > altDist)
                    {
                        distances[elem.Item1] = altDist;
                        previous[elem.Item1] = current;
                        pending.Add(elem.Item1);
                    }
                }

                yield return Tuple.Create(current, Enumerable.Empty<T>());
            }
        }
    }
    
    //En esta funcion esta haciendo time slicing, ya que el Dijkstra que hicimos UTILIZA yield
    //por lo que la funcion va haciendo "pausas" en cada yield veolviendo un elemento
    //(en este caso cada elemento es una Tupla del <current,path>, en el cual el path va a ser vacio salvo al llegar al final(si es que encuentra))
    //Se hace time Slicing al pausar y devolver el control al juego en cada elemento que nos devuelve el Dijkstra
    //En este caso se optoi por hacer la pasua cada 10 elementos
    public static IEnumerator DijkstraSlicing(int target)
    {
        var wait = new WaitForSeconds(0.05f);
        var path = Dijkstra(0, x => x == target, x => new Tuple<int, float>[] { Tuple.Create(x + 1, 1f), Tuple.Create(x + 2, 1f) });

        int count = 10;
        foreach (var elem in path)
        {
            Debug.LogWarning(elem.Item1 + " " + elem.Item2.Count());
            count--;
            if (count <= 0)
            {
                count = 10;
                yield return null;
            }
        }
        Debug.LogError("Finished");
        yield return null;
    }

    //AStar con Linq, pero sin pausas, se ejecuta y devuelde el path al final
    //o una colleccion vacia si no se encontro un path posible
    static IEnumerable<T> AStar<T>(T start,
                                           Func<T, bool> targetCheck,
                                           Func<T, IEnumerable<Tuple<T, float>>> GetNeighbours,
                                           Func<T, float> GetHeuristic)
    {
        HashSet<T> visited = new HashSet<T>();
        Dictionary<T, T> previous = new Dictionary<T, T>();
        Dictionary<T, float> actualDistances = new Dictionary<T, float>();
        Dictionary<T, float> heuristicDistances = new Dictionary<T, float>();
        List<T> pending = new List<T>();
        pending.Add(start);
        actualDistances.Add(start, 0f);
        heuristicDistances.Add(start, GetHeuristic(start));

        while (pending.Any())
        {
            var current = pending.OrderBy(x => heuristicDistances[x]).First();
            pending.Remove(current);
            visited.Add(current);

            if (targetCheck(current))
            {
                return Generate(current, x => previous[x])
                                .TakeWhile(x => previous.ContainsKey(x))
                                .Reverse();
            }
            else
            {
                var n = GetNeighbours(current).Where(x => !visited.Contains(x.Item1));
                foreach (var elem in n)
                {
                    var altDist = actualDistances[current] + elem.Item2 + GetHeuristic(elem.Item1);
                    var currentDist = heuristicDistances.ContainsKey(elem.Item1) ? heuristicDistances[elem.Item1] : float.MaxValue;

                    if (currentDist > altDist)
                    {
                        heuristicDistances[elem.Item1] = altDist;
                        actualDistances[elem.Item1] = actualDistances[current] + elem.Item2;
                        previous[elem.Item1] = current;
                        pending.Add(elem.Item1);
                    }
                }
            }
        }
        return Enumerable.Empty<T>();
    }

    //En esta funcion NO se esta haciendo time slicing, ya que el AStar que hicimos NO UTILIZA yield,
    //por lo cual al llamar a la funcion, esta se ejecutaria hasta el final
    //Solo se esta haciendo un slicing del path,es decir, una vez ya conseguido se los devuelve poco a poco
    public static IEnumerator PathSlicing<T>(T start,
                                           Func<T, bool> targetCheck,
                                           Func<T, IEnumerable<Tuple<T, float>>> GetNeighbours,
                                           Func<T, float> GetHeuristicAction,
                                           Action<T> callback)
    {
        var wait = new WaitForSeconds(0.05f);
        var path = AStar<T>(start, targetCheck, GetNeighbours, GetHeuristicAction);

        int count = 10;

        foreach (var elem in path)
        {
            Debug.LogWarning(elem);
            count--;
            if (count <= 0)
            {
                count = 10;
                yield return wait;
            }
        }
        Debug.LogError("Finished");
        yield return null;

    }


    //En este caso si se esta haciendo un time slicing directamente sobre el algoritmo de pathfing
    //fijense que en este caso, GenericSearch es un IEnumerator, NO devuelve un IEnumerable
    //El path final se devuelve en un callback que se pasa por parametro
    //En este caso se espera 0.05 segundos entre cada "nodo" a evaluar
    //Tambien se podria optar por hacerlo cada x cantidad de "nodos" chequeados
    public static IEnumerator GenericSearch<T>(T start,
                                               Func<T, bool> targetCheck,
                                               Func<T, IEnumerable<T>> GetNeighbours,
                                               Func<IEnumerable<T>, IEnumerable<T>, IEnumerable<T>> AddItems,
                                               Action<IEnumerable<T>> callback)
    {
        HashSet<T> visited = new HashSet<T>();
        IEnumerable<T> pending = new T[1] { start };
        Dictionary<T, T> parent = new Dictionary<T, T>();

        var wait = new WaitForSeconds(0.05f);

        while (pending.Any())
        {
            T current = pending.First();
            pending = pending.Skip(1);
            visited.Add(current);

            if (targetCheck(current))
            {
                var path = Generate(current, x => parent[x])
                        .TakeWhile(x => parent.ContainsKey(x))
                        .Reverse();

                callback(path);
            }
            else
            {
                var n = GetNeighbours(current).Where(x => !visited.Contains(x)).ToList();
                pending = AddItems(pending, n);
                foreach (var elem in n)
                {
                    parent.Add(elem, current);
                }

                yield return wait;
            }
        }

        callback(Enumerable.Empty<T>());
    }

    #endregion

    #region BFS, DFS, Busqueda generica

    //DFS con un poco de Linq
    public static T DFS<T>(T start, Func<T, bool> targetCheck, Func<T, IEnumerable<T>> GetNeighbours)
    {
        Stack<T> pending = new Stack<T>();
        pending.Push(start);
        HashSet<T> visited = new HashSet<T>();

        T current = default(T);

        while (pending.Any())
        {
            current = pending.Pop();
            visited.Add(current);

            if (targetCheck(current))
            {
                return current;
            }
            else
            {
                var n = GetNeighbours(current).Where(x => !visited.Contains(x));
                foreach (var elem in n)
                {
                    pending.Push(elem);
                }
            }
        }

        return default(T);
    }

    //BFS con un poco de Linq
    public static T BFS<T>(T start, Func<T, bool> targetCheck, Func<T, IEnumerable<T>> GetNeighbours)
    {
        HashSet<T> visited = new HashSet<T>();
        visited.Add(start);
        Queue<T> pending = new Queue<T>();
        pending.Enqueue(start);

        while (pending.Any())
        {
            T current = pending.Dequeue();

            if (targetCheck(current))
            {
                return current;
            }
            else
            {
                var n = GetNeighbours(current).Where(x => !visited.Contains(x));
                foreach (var elem in n)
                {
                    visited.Add(elem);
                    pending.Enqueue(elem);
                }
            }
        }

        return default(T);
    }
    //Busqueda generica pero devuelve solo el valor final
    public static T GenericIndividualSearch<T>(T start, Func<T, bool> targetCheck, Func<T, IEnumerable<T>> GetNeighbours,
                                                Func<IEnumerable<T>, IEnumerable<T>, IEnumerable<T>> AddItems)
    {
        HashSet<T> visited = new HashSet<T>();
        IEnumerable<T> pending = new T[1] { start };

        while (pending.Any())
        {
            T current = pending.First();
            pending = pending.Skip(1);
            visited.Add(current);

            if (targetCheck(current))
            {
                return current;
            }
            else
            {
                //hacemos el ToList para evitar que rompo par Side Effect, miren visited....
                var n = GetNeighbours(current).Where(x => !visited.Contains(x)).ToList(); 
                pending = AddItems(pending, n);
            }
        }

        return default(T);
    }
    //Busqueda generica con Linq, pero sin "pausas"
    public static IEnumerable<T> GenericSearch<T>(T start, Func<T, bool> targetCheck, Func<T, IEnumerable<T>> GetNeighbours,
                                                Func<IEnumerable<T>, IEnumerable<T>, IEnumerable<T>> AddItems)
    {
        HashSet<T> visited = new HashSet<T>();
        IEnumerable<T> pending = new T[1] { start };
        Dictionary<T, T> parent = new Dictionary<T, T>();

        while (pending.Any())
        {
            T current = pending.First();
            pending = pending.Skip(1);
            visited.Add(current);

            if (targetCheck(current))
            {
                return Generate(current, x => parent[x])
                        .TakeWhile(x => parent.ContainsKey(x))
                        .Reverse();
            }
            else
            {
                var n = GetNeighbours(current).Where(x => !visited.Contains(x)).ToList();
                pending = AddItems(pending, n);
                foreach (var elem in n)
                {
                    parent.Add(elem, current);
                }
            }
        }

        return Enumerable.Empty<T>();
    }


  
   
    

    #endregion
}
