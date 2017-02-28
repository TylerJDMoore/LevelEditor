using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

public class LevelEditor : MonoBehaviour
{
	private GameObject cam; //Camera GameObject, used for raycasts
	public Material cursorMaterial; //Transparent capable material
	private Mesh cursorMesh; //Cube mesh used for the cursor, slighly larger than 1x1x1 to prevent clipping
	public List<GameObject> blocks = new List<GameObject>(); //List of placeable blocks
	private int blockIndex; //Starting block, default value of 0
	private bool isDestroying; //Starting in block placing mode, default value of false
	private Vector3 position; //Cursor position
	private Vector3 rotation = Vector3.zero; //Starting block orientation
	public string levelName = "Untitled"; //Default file name
	public string extension = "map"; //Default file extension
	private bool doRender; //Whether the cursor should be rendered

	void Start()
	{
		//Set starting values
		cam = transform.GetChild(0).gameObject;
		position = new Vector3(0, 0.5f, 0);
		GetComponent<CapsuleCollider>().enabled = false;
		cursorMaterial.color = new Color(0, 1, 23 / 255, 0.5f);

		//Generate mesh
		CreateCube();
	}

	void Update()
	{
		//Save and load level
		if (Input.GetKeyDown(KeyCode.O))
			SaveLevel();
		if (Input.GetKeyDown(KeyCode.P))
			LoadLevel();
		if (Input.GetKeyDown(KeyCode.I))
			ClearLevel();

		if (Input.GetMouseButtonDown(1))
			ChangePlaceMode();

		if (!isDestroying)
			HandleModifyBlockPlacing();

		//Disable character controller in order to perform raycast
		GetComponent<CharacterController>().enabled = false;

		DoRaycast();

		//Render cursor
		if (doRender)
			Graphics.DrawMesh(cursorMesh, position, Quaternion.Euler(rotation), cursorMaterial, 0, null, 0, null, false, false);

		//Re-enable character controller to allow collisions
		GetComponent<CharacterController>().enabled = true;
	}


	void OnGUI()
	{
		//Draw info and instructions
		GUI.Label(new Rect(10, 10, 256, 32), "Save level (O)");
		GUI.Label(new Rect(10, 30, 256, 32), "Load level (P)");
		GUI.Label(new Rect(10, 50, 256, 32), "Clear level (I)");
		GUI.Label(new Rect(10, 70, 256, 32), "Scroll to change current block");
		GUI.Label(new Rect(10, 90, 256, 32), "Rotate block placement with (Q) and (E)");
		GUI.Label(new Rect(10, 110, 256, 32), "Currently placing: " + blocks[blockIndex].name);
	}

	private void ChangePlaceMode()
	{
		isDestroying = !isDestroying;

		//Set cursor colour
		if (isDestroying)
			cursorMaterial.color = new Color(1, 0, 0, 0.5f);
		else
			cursorMaterial.color = new Color(0, 1, 23 / 255, 0.5f);
	}

	private void HandleModifyBlockPlacing()
	{
		//Rotate block
		if (Input.GetKeyDown(KeyCode.E))
			rotation += Vector3.up * 90;
		if (Input.GetKeyDown(KeyCode.Q))
			rotation += Vector3.down * 90;

		//Change selected block
		float scroll = Input.GetAxis("Mouse ScrollWheel");
		if (scroll < 0)
		{
			//Select previous block
			blockIndex--;
			if (blockIndex < 0)
				blockIndex = blocks.Count - 1;
		}
		else if (scroll > 0)
		{
			//Select next block
			blockIndex++;
			if (blockIndex > blocks.Count - 1)
				blockIndex = 0;
		}
	}

	private void DoRaycast()
	{
		RaycastHit hit;
		if (Physics.Raycast(cam.GetComponent<Camera>().transform.position, cam.GetComponent<Camera>().transform.forward, out hit))
		{
			//Hit something
			doRender = true;
			HandleRaycast(hit);

			//Enable the capsule collider to allow for collision with the cursor
			GetComponent<CapsuleCollider>().enabled = true;
			//Get collisions at target position
			Collider[] cols = Physics.OverlapBox(position, new Vector3(0.49f, 0.49f, 0.49f));

			if (Input.GetMouseButtonDown(0))
				DoInteract(cols);

			foreach (Collider c in cols)
			{
				//Level editor has tag "Player"
				//Don't render if cursor touches the level editor
				if (c.transform.CompareTag("Player"))
					doRender = false;
			}
			//Disable the capsule collider used for collision with the cursor
			GetComponent<CapsuleCollider>().enabled = false;
		}
		else
			doRender = false;
	}

	private void HandleRaycast(RaycastHit hit)
	{
		Debug.DrawLine(cam.GetComponent<Camera>().transform.position, hit.point, Color.blue);
		if (isDestroying)
		{
			//Set position of cursor for block destroy mode
			if (Mathf.Abs(hit.transform.position.y) > 1e-8f)
				position = hit.transform.root.position;
			else
				position = new Vector3(Mathf.Round(hit.point.x), 0.5f, Mathf.Round(hit.point.z));
		}
		else
		{
			//Set position of cursor for block place mode
			if (Mathf.Abs(hit.transform.position.y) > 1e-8f)
				position = hit.transform.root.position + hit.normal;
			else
				position = new Vector3(Mathf.Round(hit.point.x), 0.5f, Mathf.Round(hit.point.z));
		}
	}

	private void DoInteract(Collider[] cols)
	{
		if (isDestroying)
		{
			//Destroy all blocks selected by cursor
			foreach (Collider c in cols)
			{
				if (c.transform.CompareTag("Block"))
				{
					Destroy(c.gameObject);
				}
			}
		}
		else
		{
			//If cursor location is free, instantiate block
			if (cols.Length == 0)
			{
				GameObject current = Instantiate(blocks[blockIndex]);
				current.transform.position = position;
				current.transform.rotation = Quaternion.Euler(rotation);
				current.transform.eulerAngles = rotation;
				current.AddComponent(typeof(Block));
				current.GetComponent<Block>().blockID = blockIndex;
			}
		}
	}

	private void SaveLevel()
	{
		//Save level to file
		BinaryFormatter binaryFormatter = new BinaryFormatter();
		FileStream file;

		//Open file if it exists, otherwise create it
		if (File.Exists(Application.persistentDataPath + "/" + levelName + "." + extension))
			file = File.Open(Application.persistentDataPath + "/" + levelName + "." + extension, FileMode.Open);
		else
			file = File.Create(Application.persistentDataPath + "/" + levelName + "." + extension);

		//Save data to level
		Level level = new Level(transform.position, GameObject.FindGameObjectsWithTag("Block"));

		//Save level to file
		binaryFormatter.Serialize(file, level);
		file.Close();

		print("Saved level to " + Application.persistentDataPath + "/" + levelName + "." + extension + " at " + System.DateTime.Now.Hour.ToString("00") + ":" + System.DateTime.Now.Minute.ToString("00") + ":" + System.DateTime.Now.Second.ToString("00"));
	}

	private void LoadLevel()
	{
		//Empty the level of all blocks
		foreach (GameObject g in GameObject.FindGameObjectsWithTag("Block"))
			Destroy(g);
		//Open file if it exists
		if (File.Exists(Application.persistentDataPath + "/" + levelName + "." + extension))
		{
			BinaryFormatter binaryFormatter = new BinaryFormatter();
			FileStream file = File.Open(Application.persistentDataPath + "/" + levelName + "." + extension, FileMode.Open);
			Level level = (Level)binaryFormatter.Deserialize(file);
			file.Close();

			//Instantiate blocks from saved file
			foreach (SerializableBlock block in level.obj)
			{
				GameObject g = Instantiate(blocks[block.blockIndex]);
				g.transform.position = block.position.ToVector3();
				g.transform.eulerAngles = block.rotation.ToVector3(); //todo: use int for orientations

				g.AddComponent(typeof(Block));
				g.GetComponent<Block>().blockID = block.blockIndex;
			}

			//Set player position
			transform.position = level.location.ToVector3();
			print("Instantiated " + level.obj.Count + " level objects");
		}
		else
			print(Application.persistentDataPath + "/" + levelName + "." + extension + " does not exist");
	}

	private void ClearLevel()
	{
		//Empty the level of all blocks
		foreach (GameObject g in GameObject.FindGameObjectsWithTag("Block"))
			Destroy(g);
	}

	private void CreateCube()
	{
		//Generate vertices
		float i = 0.505f;
		Vector3[] vertices = {
            //Front face
            new Vector3(-i, -i, -i),
			new Vector3(-i, i, -i),
			new Vector3(i, -i, -i),
			new Vector3(i, i, -i),
            
            //Right face
            new Vector3(i, -i, -i),
			new Vector3(i, i, -i),
			new Vector3(i, -i, i),
			new Vector3(i, i, i),
            
            //Back face
            new Vector3(i, -i, i),
			new Vector3(i, i, i),
			new Vector3(-i, -i, i),
			new Vector3(-i, i, i),
            
            //Left face
            new Vector3(-i, -i, i),
			new Vector3(-i, i, i),
			new Vector3(-i, -i, -i),
			new Vector3(-i, i, -i),
            
            //Top face
            new Vector3(-i, i, -i),
			new Vector3(-i, i, i),
			new Vector3(i, i, -i),
			new Vector3(i, i, i),
            
            //Bottom face
            new Vector3(-i, -i, i),
			new Vector3(-i, -i, -i),
			new Vector3(i, -i, i),
			new Vector3(i, -i, -i)

		};

		//Generate triangles
		int[] tris = {
			0, 1, 2,
			1, 3, 2,

			4, 5, 6,
			5, 7, 6,

			8, 9, 10,
			9, 11, 10,

			12, 13, 14,
			13, 15, 14,

			16, 17, 18,
			17, 19, 18,

			20, 21, 22,
			21, 23, 22
		};

		cursorMesh = new Mesh();
		cursorMesh.Clear();
		cursorMesh.vertices = vertices;
		cursorMesh.triangles = tris;
		cursorMesh.RecalculateNormals();
	}
}

[Serializable]
class Level
{
	//Serializable level with player location and stored values for blocks
	public SerializableVector3 location;
	public List<SerializableBlock> obj;

	public Level(Vector3 location, GameObject[] blocks)
	{
		this.location = new SerializableVector3(location);
		obj = new List<SerializableBlock>();
		foreach (GameObject block in blocks)
		{
			obj.Add(new SerializableBlock(block.transform.position, block.transform.eulerAngles, block.GetComponent<Block>().blockID));
		}
	}
}

[Serializable]
class SerializableBlock
{
	//Serializable block with position, rotation, and int storing the block index
	public SerializableVector3 position, rotation;
	public int blockIndex;
	public SerializableBlock(Vector3 pos, Vector3 rot, int blockIndex)
	{
		this.position = new SerializableVector3(pos);
		this.rotation = new SerializableVector3(rot);
		this.blockIndex = blockIndex;
	}
}

[Serializable]
class SerializableVector3
{
	//Allows for serialization of Vector3s
	public float x, y, z;
	public SerializableVector3(Vector3 v)
	{
		this.x = v.x;
		this.y = v.y;
		this.z = v.z;
	}

	public Vector3 ToVector3()
	{
		//Return stored data as a Vector3
		return new Vector3(x, y, z);
	}
}

public class Block : MonoBehaviour
{
	//Stores block id for saving and instantiating
	public int blockID;
}

