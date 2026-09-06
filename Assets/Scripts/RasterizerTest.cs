using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RasterizerTest : MonoBehaviour {

	public float forwardOffset;
	public float rayLength;
	public bool useType2;
	public bool checkSubBlocks;
	public bool useType1 = true;
	private List<Vector2Int> points;
	private List<Vector2Int> subPoints;
	private Transform cubeParent0;
	private Transform cubeParent1;
	private GameObject cube;
	private GameObject subCube;
	private List<Vector3Int> points3d;
	private List<Vector3Int> subPoints3d;

	void Awake()
	{
		points = new List<Vector2Int>();
//		subPoints = new List<Vector2Int>();
		subPoints = points;
		cubeParent0 = GameObject.Find("CubeParent0").transform;
		cubeParent1 = GameObject.Find("CubeParent1").transform;
		cube = Resources.Load("cell0") as GameObject;
		subCube = Resources.Load("cell1") as GameObject;
		points3d = new List<Vector3Int>();
		subPoints3d = new List<Vector3Int>();
	}

	void Update()
	{
		GameObject[] cells = GameObject.FindGameObjectsWithTag("Cell0");
		foreach(GameObject cell in cells)
			GameObject.Destroy(cell);

		Vector3 start = transform.position + transform.forward * forwardOffset;
		Vector3 end = start + transform.forward * rayLength;

/*
		// X-Z Plane
		Rasterizer2.Line2D(points, subPoints, new Vector2(start.x, start.z), new Vector2(end.x, end.z), checkSubBlocks);

		Debug.DrawLine(start, end, Color.green, 0.1f);
		float y = transform.position.y;
		foreach(Vector2Int p in points){
			GameObject t = (GameObject)GameObject.Instantiate(cube, new Vector3(p.x, y, p.y), Quaternion.identity);
			t.transform.parent = cubeParent0;
		}
		foreach(Vector2Int p in subPoints){
			GameObject t = (GameObject)GameObject.Instantiate(subCube, new Vector3(p.x, y, p.y), Quaternion.identity);
			t.transform.parent = cubeParent1;	
		}*/
		/*
		// X-Y Plane
		Rasterizer2.Line2D(points, subPoints, new Vector2(start.x, start.y), new Vector2(end.x, end.y));

		float z = transform.position.z;
		foreach(Vector2Int p in points){
			GameObject t = (GameObject)GameObject.Instantiate(cube, new Vector3(p.x, p.y, z), Quaternion.identity);
			t.transform.parent = cubeParent0;
		}
		foreach(Vector2Int p in subPoints){
			GameObject t = (GameObject)GameObject.Instantiate(subCube, new Vector3(p.x, p.y, z), Quaternion.identity);
			t.transform.parent = cubeParent1;	
		}

*/

//		Rasterizer2.Line3D(points3d, subPoints3d, start, end);
//		Rasterizer2.Line3D(ref points3d, start, end, checkSubBlocks);
		foreach(Vector3Int p in points3d){
			GameObject t = (GameObject)GameObject.Instantiate(cube, new Vector3(p.x, p.y, p.z), Quaternion.identity);
			t.transform.parent = cubeParent0;
		}
		foreach(Vector3Int p in subPoints3d){
			GameObject t = (GameObject)GameObject.Instantiate(subCube, new Vector3(p.x, p.y, p.z), Quaternion.identity);
			t.transform.parent = cubeParent1;	
		}
		
		Debug.DrawLine(start, end, Color.blue, 0.1f);
	
	}

}
