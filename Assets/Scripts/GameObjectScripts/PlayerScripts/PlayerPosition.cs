using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerPosition : MonoBehaviour
{
    [SerializeField] Grid gameboard;
    [SerializeField] Tilemap map;
    public Vector3Int gridPos;
    public Vector3 position;
    public Dictionary<Directions, Vector3Int> compass = new()
    {
        {Directions.Northwest, new Vector3Int(-1,1)},
        {Directions.Northeast, new Vector3Int(0,1)},
        {Directions.East, new Vector3Int(1,0)},
        {Directions.Southeast, new Vector3Int(0,-1)},
        {Directions.Southwest, new Vector3Int(-1,-1)},
        {Directions.West, new Vector3Int(-1,0)}
    };
    public bool inCombat;

    private void Start()
    {
    }
    private void Update()
    {
        gridPos = gameboard.LocalToCell(transform.position);
        position = transform.position;
        // if (!player.InCombat)
        // {
        //     if(Input.GetAxis("Mouse ScrollWheel") < 0f && Camera.main.orthographicSize < 6f)
        //     {
        //         Camera.main.orthographicSize += 20 * Time.deltaTime;
        //     }
        //     if(Input.GetAxis("Mouse ScrollWheel") > 0f && Camera.main.orthographicSize > 1.5f)
        //     {
        //         Camera.main.orthographicSize -= 20 * Time.deltaTime;
        //     }
        // }
    }

    public void UpdateCompass(Vector3Int vector, Dictionary<Directions, Vector3Int> compass)
    {
        if(vector.y % 2 != 0)
        {
            compass[Directions.Northwest] = new Vector3Int(0,1);
            compass[Directions.Northeast] = new Vector3Int(1,1);
            compass[Directions.Southwest] = new Vector3Int(0,-1);
            compass[Directions.Southeast] = new Vector3Int(1,-1);
        }
        else
        {
            compass[Directions.Northwest] = new Vector3Int(-1,1);
            compass[Directions.Northeast] = new Vector3Int(0,1);
            compass[Directions.Southwest] = new Vector3Int(-1,-1);
            compass[Directions.Southeast] = new Vector3Int(0,-1);
        }
    }
}
