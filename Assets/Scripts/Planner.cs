using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

public class Planner : MonoBehaviour
{
    private readonly List<Tuple<Vector3, Vector3>> _debugRayList = new List<Tuple<Vector3, Vector3>>();

    private void Start()
    {
        StartCoroutine(Plan());
    }

    private void Check(Dictionary<string, bool> state, ItemType type)
    {

        var items = Navigation.instance.AllItems();
        var inventories = Navigation.instance.AllInventories();
        var floorItems = items.Except(inventories);//devuelve una coleccion como la primera pero removiendo los que estan en la segunda
        var item = floorItems.FirstOrDefault(x => x.type == type);
        var here = transform.position;
        state["accessible" + type.ToString()] = item != null && Navigation.instance.Reachable(here, item.transform.position, _debugRayList);

        var inv = inventories.Any(x => x.type == type);
        state["otherHas" + type.ToString()] = inv;

        state["dead" + type.ToString()] = false;
    }

    private IEnumerator Plan()
    {
        yield return new WaitForSeconds(0.2f);

        var observedState = new Dictionary<string, bool>();

        var nav = Navigation.instance;//Consigo los items
        var floorItems = nav.AllItems();
        var inventory = nav.AllInventories();
        var everything = nav.AllItems().Union(nav.AllInventories());// .Union() une 2 colecciones sin agregar duplicados(eso incluye duplicados en la misma coleccion)

        //Chequeo los booleanos para cada Item, generando mi modelo de mundo (mi diccionario de bools) en ObservedState
        //chequea cada itemtype en caso de agregar algo
        Check(observedState, ItemType.Key);
        Check(observedState, ItemType.Entity);
        Check(observedState, ItemType.Mace);
        Check(observedState, ItemType.PastaFrola);
        Check(observedState, ItemType.Door);

        var actions = CreatePossibleActionsList();


        GoapState initial = new GoapState();
        initial.worldState = new WorldState()
        {
            playerHP = 88,
            values = new Dictionary<string, bool>()
        };

        initial.worldState.values = observedState; //le asigno los valores actuales, conseguidos antes
        initial.worldState.values["doorOpen"] = false; //agrego el bool "doorOpen"

        foreach (var item in initial.worldState.values)
        {
            Debug.Log(item.Key + " ---> " + item.Value);
        }

        GoapState goal = new GoapState();
        //goal.values["has" + ItemType.Key.ToString()] = true;
        goal.worldState.values["has" + ItemType.PastaFrola.ToString()] = true;
        //goal.values["has"+ ItemType.Mace.ToString()] = true;
        //goal.values["dead" + ItemType.Entity.ToString()] = true;}


        Func<GoapState, float> heuristc = (curr) =>
        {
            int count = 0;
            string key = "has" + ItemType.PastaFrola.ToString();
            if (!curr.worldState.values.ContainsKey(key) || !curr.worldState.values[key])
                count++;
            if (curr.worldState.playerHP <= 45)
                count++;
            return count;
        };

        Func<GoapState, bool> objectice = (curr) =>
         {
             string key = "has" + ItemType.PastaFrola.ToString();
             return curr.worldState.values.ContainsKey(key) && curr.worldState.values["has" + ItemType.PastaFrola.ToString()]
                    && curr.worldState.playerHP > 45;
         };




        var actDict = new Dictionary<string, ActionEntity>() {
              { "Kill"  , ActionEntity.Kill }
            , { "Pickup", ActionEntity.PickUp }
            , { "Open"  , ActionEntity.Open }
        };

        var plan = Goap.Execute(initial, null, objectice, heuristc, actions);

        if (plan == null)
            Debug.Log("Couldn't plan");
        else
        {
            GetComponent<Guy>().ExecutePlan(
                plan
                .Select(a =>
                {
                    Item i2 = everything.FirstOrDefault(i => i.type == a.item);
                    if (actDict.ContainsKey(a.Name) && i2 != null)
                    {
                        return Tuple.Create(actDict[a.Name], i2);
                    }
                    else
                    {
                        return null;
                    }
                }).Where(a => a != null)
                .ToList()
            );
        }
    }
    //aca seteariamos la accion con el landa, aca ya lo hice
    private List<GoapAction> CreatePossibleActionsList()
    {
        return new List<GoapAction>()
        {
            new GoapAction("Kill")
        .SetCost(3f)
        .Pre((m) => m.worldState.alive && m.worldState.weapon == "espada")
        .Effect((m) => { m.worldState.coins += 10; return m; }),

             new GoapAction("RecogerMonedas")
        .SetCost(1f)
        .Pre((m) => m.worldState.alive && m.worldState.coins < 10)
        .Effect((m) => { m.worldState.coins += 5; return m; }),


         new GoapAction("Curarse")
        .SetCost(2f)
        .Pre((m) => m.worldState.alive && m.worldState.playerHP < 50)
        .Effect((m) => { m.worldState.playerHP += 20; return m; }),

         new GoapAction("CambiarArma")
        .SetCost(1.5f)
        .Pre((m) => m.worldState.alive && m.worldState.weapon != "espada")
        .Effect((m) => { m.worldState.weapon = "espada"; return m; }),


        new GoapAction("Morir")
        .SetCost(0f) 
        .Pre((m) => m.worldState.alive && m.worldState.playerHP <= 0)
        .Effect((m) => { m.worldState.alive = false; return m; })

            #region ejemplos
            //              new GoapAction("Kill")
            //                .SetCost(1f)
            //                .SetItem(ItemType.Entity)
            //                .Pre((gS)=>
            //                {
            //                    //Esto es un ejemplo exajerado, podriamos tener una convinacion, y en las precondiciones tener un diccionbario, como antes y aca chequear
            //                    return gS.worldState.values.ContainsKey("dead"+ ItemType.Entity.ToString()) &&
            //                           gS.worldState.values.ContainsKey("accessible"+ ItemType.Entity.ToString()) &&
            //                           gS.worldState.values.ContainsKey("has"+ ItemType.Mace.ToString()) &&
            //                           !gS.worldState.values["dead"+ ItemType.Entity.ToString()] &&
            //                           gS.worldState.values["accessible"+ ItemType.Entity.ToString()] &&
            //                           gS.worldState.values["has"+ ItemType.Mace.ToString()] &&

            //                           gS.worldState.playerHP > 50;
            //                })
            //                //Ejemplo de setteo de Effect
            //                .Effect((gS) =>
            //                    {
            //                        gS.worldState.values["dead"+ ItemType.Entity.ToString()] = true;
            //                        gS.worldState.values["accessible"+ ItemType.Key.ToString()] = true;
            //                        return gS;
            //                    }
            //                )

            //            , new GoapAction("Loot")
            //                .SetCost(1f)
            //                .SetItem(ItemType.Key)
            //                .Pre("otherHas"+ ItemType.Key.ToString(), true)
            //                .Pre("dead"+ ItemType.Entity.ToString(), true)

            //                .Effect("accessible"+ ItemType.Key.ToString(), true)
            //                .Effect("otherHas"+ ItemType.Key.ToString(), false)

            //            , new GoapAction("Pickup")
            //                .SetCost(2f)
            //                .SetItem(ItemType.Mace)
            //                .Pre("dead"+ ItemType.Mace.ToString(), false)
            //                .Pre("otherHas"+ ItemType.Mace.ToString(), false)
            //                .Pre("accessible"+ ItemType.Mace.ToString(), true)

            //                .Effect("accessible"+ ItemType.Mace.ToString(), false)
            //                .Effect("has"+ ItemType.Mace.ToString(), true)

            //            , new GoapAction("Pickup")
            //                .SetCost(2f)
            //                .SetItem(ItemType.Key)
            ////                .Pre("deadKey", false)
            ////                .Pre("otherHasKey", false)
            //                .Pre("accessible"+ ItemType.Key.ToString(), true)

            //                .Effect("accessible"+ ItemType.Key.ToString(), false)
            //                .Effect("has"+ ItemType.Key.ToString(), true)

            //            , new GoapAction("Pickup")
            //                .SetCost(5f)					//La frola es prioritaria!
            //                .SetItem(ItemType.PastaFrola)
            //				.Pre("dead"+ ItemType.PastaFrola.ToString(), false)
            //                .Pre("otherHas"+ ItemType.PastaFrola.ToString(), false)
            //                .Pre("accessible"+ ItemType.PastaFrola.ToString(), true)
            //                //.Pre("hasKey",true)

            //                .Effect("accessible"+ ItemType.PastaFrola.ToString(), false)
            //                .Effect("has"+ ItemType.PastaFrola.ToString(), true)

            //            , new GoapAction("Open")
            //                .SetCost(3f)
            //                .SetItem(ItemType.Door)
            //                .Pre("dead"+ ItemType.Door.ToString(), false)
            //                .Pre("has"+ ItemType.Key.ToString(), true)

            //                .Effect("has"+ ItemType.Key.ToString(), false)
            //                .Effect("doorOpen", true)
            //                .Effect("dead"+ ItemType.Key.ToString(), true)
            //                .Effect("accessible"+ ItemType.PastaFrola.ToString(), true)

            //                , new GoapAction("Kill")
            //                .SetCost(20f)
            //                .SetItem(ItemType.Door)
            //                .Pre("dead"+ ItemType.Door.ToString(), false)
            //                .Pre("has"+ ItemType.Mace.ToString(), true)

            //                .Effect("doorOpen", true)
            //                .Effect("has"+ ItemType.Mace.ToString(), false)
            //                .Effect("dead"+ ItemType.Mace.ToString(), true)
            //                .Effect("dead"+ ItemType.Door.ToString(), true)
            //                .Effect("accessible"+ ItemType.PastaFrola.ToString(), true)
#endregion

        };
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        foreach (var t in _debugRayList)
        {
            Gizmos.DrawRay(t.Item1, (t.Item2 - t.Item1).normalized);
            Gizmos.DrawCube(t.Item2 + Vector3.up, Vector3.one * 0.2f);
        }
    }
}
