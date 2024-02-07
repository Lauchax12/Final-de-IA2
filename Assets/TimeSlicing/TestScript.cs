using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestScript : MonoBehaviour
{
    GameObject prefab;
    private void Awake()
    {
        Debug.Log("Start");
    }
    void Start()
    {
        int target = 10000;
        StartCoroutine(TimeSlicing.DijkstraSlicing(target));
        //StartCoroutine(TimeSlicing.PathSlicing(0, x => x == target, x => new Tuple<int, float>[] { Tuple.Create(x + 1, 1f), Tuple.Create(x + 2, 1f) },x=> Mathf.Abs(target - x),(m) => Debug.LogWarning(m)));
    }
   
    void Update()
    {
        Debug.Log("Update");
    }
}
