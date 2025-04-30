using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildManager : MonoBehaviour
{
    public float pieceLength = 0.8f;
    public float pieceHeight = 2f;
    public GameObject[] FirstFloorWallPieces;
    public GameObject[] RedFloorWallPieces;
    public GameObject[] MortarFloorWallPieces;
    public int minWallAmount = 4;
    public int maxWallAmount = 10;

    public int minHeightAmount = 3;
    public int maxHeightAmount = 8;

    int frontSize;
    int sideSize;
    int floorAmount;

    int CalculateSize(int min, int max)
    {
        int number = Random.Range(min, max + 1);
        return number;
    }

    void Start()
    {
        CreateBuilding();

      
    }

    public void CreateBuilding()
    {
        frontSize = CalculateSize(minWallAmount, maxWallAmount);
        sideSize = CalculateSize(minWallAmount, maxWallAmount);
        floorAmount = CalculateSize(minHeightAmount, maxHeightAmount);


        GameObject newBuilding = new GameObject();
        newBuilding.transform.position = Vector3.zero;
        newBuilding.name = "New Building";

        for (int i = 0; i <floorAmount; i++)
        {
            if(i == 0)
            {
                CreateFloor(newBuilding, FirstFloorWallPieces, 0);
            }
            else
            {
                CreateFloor(newBuilding, RedFloorWallPieces, (i * pieceHeight));
                CreateFloor(newBuilding, MortarFloorWallPieces, (i * pieceHeight));
            }
        }

    }

    public void CreateFloor(GameObject newBuilding, GameObject[] floorPiecesToUse, float floorHeight)
    {
        

        //The First Front Wall
        for (int i = 0; i < frontSize; i++) 
        {
            Vector3 pos = newBuilding.transform.position + new Vector3( pieceLength / 2 + (i*pieceLength), floorHeight, 0);
            int randIndex = Random.Range(0, floorPiecesToUse.Length);
            GameObject newWallPiece = Instantiate(floorPiecesToUse[randIndex], pos, Quaternion.identity, newBuilding.transform);


        }

        //The First Back Wall
        for (int i = 0; i < frontSize; i++)
        {
            Vector3 pos = newBuilding.transform.position + new Vector3(pieceLength / 2 + (i * pieceLength), floorHeight, sideSize * pieceLength);
            int randIndex = Random.Range(0, floorPiecesToUse.Length);
            GameObject newWallPiece = Instantiate(floorPiecesToUse[randIndex], pos, Quaternion.Euler(0,180,0), newBuilding.transform);


        }

        //The First Left Wall
        for (int i = 0; i < sideSize; i++)
        {
            Vector3 pos = newBuilding.transform.position + new Vector3(0, floorHeight,pieceLength / 2 + (i * pieceLength));
            int randIndex = Random.Range(0, floorPiecesToUse.Length);
            GameObject newWallPiece = Instantiate(floorPiecesToUse[randIndex], pos, Quaternion.Euler(0, 90, 0), newBuilding.transform);


        }

        //The First Right Wall
        for (int i = 0; i < sideSize; i++)
        {
            Vector3 pos = newBuilding.transform.position + new Vector3(frontSize * pieceLength, floorHeight, pieceLength / 2 + (i * pieceLength));
            int randIndex = Random.Range(0, floorPiecesToUse.Length);
            GameObject newWallPiece = Instantiate(floorPiecesToUse[randIndex], pos, Quaternion.Euler(0, -90, 0), newBuilding.transform);


        }
    }

    



}
