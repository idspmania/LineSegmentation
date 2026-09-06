using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ForceMasters{

    public class DebugUtils{
		public static void InstantiateCubes(List<Vector3> points, string cubeTag)
		{
			GameObject[] cubes = GameObject.FindGameObjectsWithTag(cubeTag);
			foreach(GameObject cube in cubes)
				GameObject.Destroy(cube);

			Transform parent = GameObject.Find("_Scene").transform.Find("CellParent0");
			GameObject b = Resources.Load("cell0") as GameObject;
			foreach(Vector3 p in points){
				GameObject t = (GameObject)GameObject.Instantiate(b, p, Quaternion.identity);
				t.transform.parent = parent;
			}
		}

		public static void InstantiateCubes(List<Vector3Int> points, string cubeTag)
		{
			GameObject[] cubes = GameObject.FindGameObjectsWithTag(cubeTag);
			foreach(GameObject cube in cubes)
				GameObject.Destroy(cube);

			Transform parent = GameObject.Find("_Scene").transform.Find("CellParent0");
			GameObject b = Resources.Load(cubeTag.ToLower()) as GameObject;
			foreach(Vector3Int p in points){
				GameObject t = (GameObject)GameObject.Instantiate(b, (Vector3)p, Quaternion.identity);
				t.transform.parent = parent;
			}
		}

		public static void InstantiateCubes(ref Vector3Int[] points, int cnt, string cubeTag)
		{
			GameObject[] cubes = GameObject.FindGameObjectsWithTag(cubeTag);
			foreach (GameObject cube in cubes)
				GameObject.Destroy(cube);

			Transform parent = GameObject.Find("_Scene").transform.Find("CellParent0");
			GameObject b = Resources.Load(cubeTag.ToLower()) as GameObject;
			for(int i = 0; i < cnt; i++) {
				GameObject t = (GameObject)GameObject.Instantiate(b, (Vector3)points[i], Quaternion.identity);
				t.transform.parent = parent;
			}
		}

		public static void InstantiateCubes(List<Vector3Int> points1, List<Vector3Int> points2, string cubeTag)
		{
			GameObject[] cubes = GameObject.FindGameObjectsWithTag(cubeTag);
			foreach (GameObject cube in cubes)
				GameObject.Destroy(cube);

			Transform parent = GameObject.Find("_Scene").transform.Find("CellParent0");
			GameObject b = Resources.Load(cubeTag.ToLower()) as GameObject;
			foreach (Vector3Int p in points1) {
				GameObject t = (GameObject)GameObject.Instantiate(b, (Vector3)p, Quaternion.identity);
				t.transform.parent = parent;
			}
			foreach (Vector3Int p in points2) {
				GameObject t = (GameObject)GameObject.Instantiate(b, (Vector3)p, Quaternion.identity);
				t.transform.parent = parent;
			}
		}

		public static void InstantiateCubes(ref Vector3Int[] points1, int cnt1, ref Vector3Int[] points2, int cnt2, string cubeTag)
		{
			GameObject[] cubes = GameObject.FindGameObjectsWithTag(cubeTag);
			foreach (GameObject cube in cubes)
				GameObject.Destroy(cube);

			Transform parent = GameObject.Find("_Scene").transform.Find("CellParent0");
			GameObject b = Resources.Load(cubeTag.ToLower()) as GameObject;
			for(int i = 0; i < cnt1; i++) {
				GameObject t = (GameObject)GameObject.Instantiate(b, (Vector3)points1[i], Quaternion.identity);
				t.transform.parent = parent;
			}
			for(int i = 0; i < cnt2; i++) { 
				GameObject t = (GameObject)GameObject.Instantiate(b, (Vector3)points2[i], Quaternion.identity);
				t.transform.parent = parent;
			}
		}

		public static void InstantiateCubesFloat(List<Vector3> points, string cubeTag)
		{
			GameObject[] cubes = GameObject.FindGameObjectsWithTag(cubeTag);
			foreach (GameObject cube in cubes)
				GameObject.Destroy(cube);

			Transform parent = GameObject.Find("_Scene").transform.Find("CellParent0");
			GameObject b = Resources.Load(cubeTag.ToLower()) as GameObject;
			b.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
			foreach (Vector3 p in points) {
				GameObject t = (GameObject)GameObject.Instantiate(b, (Vector3)p, Quaternion.identity);
				t.transform.parent = parent;
			}
		}

		public static void ShowCallerInfo(string message,
		[System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
		[System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
		[System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
		{
			Debug.Log($"message: {message}");
			Debug.Log($"member name: {memberName}");
			Debug.Log($"source file path: {sourceFilePath}");
			Debug.Log($"source line number: {sourceLineNumber}");
		}
	}
}
	