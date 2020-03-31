﻿/*
MIT License

Copyright (c) 2020 Steffen Vetne

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityExtensions;

[CreateAssetMenu(menuName = "SurfaceSounds")]
public class SurfaceSounds : ScriptableObject
{
    //Fields
    [Space(50)]
    [Tooltip("If it can't find one")]
    public int defaultSurfaceType = 0;
#if UNITY_EDITOR
    public string autoDefaultSurfaceTypeHeader;
#endif

    [Space(50)]
    [ReorderableList()]
    public SurfaceType[] surfaceTypes = new SurfaceType[] { new SurfaceType() };
    [ReorderableList()]

    [Space(50)]
    public string[] soundSetNames = new string[] { "Human", "Heavy" };



    //Methods
    public int FindSoundSetID(string name)
    {
        for (int i = 0; i < soundSetNames.Length; i++)
            if (name == soundSetNames[i])
                return i;

        throw new System.Exception("No sound set with name: " + name);
    }


    public SurfaceType GetSphereCastSurfaceType(Vector3 worldPosition, Vector3 downDirection, float radius = 1, float maxDistance = Mathf.Infinity, int layerMask = -1)
    {
        if (Physics.SphereCast(worldPosition, radius, downDirection, out RaycastHit rh, maxDistance, layerMask, QueryTriggerInteraction.Ignore))
        {
#if UNITY_EDITOR
            var bottomCenter = worldPosition + downDirection * rh.distance;
            Debug.DrawLine(worldPosition, bottomCenter);
            Debug.DrawLine(bottomCenter, rh.point);
#endif

            return GetSurfaceType(rh.collider, worldPosition, rh.triangleIndex);
        }

        return GetSurfaceType(null, worldPosition);
    }
    public SurfaceType GetRaycastSurfaceType(Vector3 worldPosition, Vector3 downDirection, float maxDistance = Mathf.Infinity, int layerMask = -1)
    {
        if (Physics.Raycast(worldPosition, downDirection, out RaycastHit rh, maxDistance, layerMask, QueryTriggerInteraction.Ignore))
        {
#if UNITY_EDITOR
            Debug.DrawLine(worldPosition, rh.point);
#endif

            return GetSurfaceType(rh.collider, worldPosition, rh.triangleIndex);
        }

        return GetSurfaceType(null, worldPosition);
    }
    public SurfaceType GetCollisionSurfaceType(Collision collision)
    {
        return GetSurfaceType(collision.collider, collision.GetContact(0).point);
    }

    public bool TryGetStringSurfaceType(string checkName, out SurfaceType st)
    {
        if (!System.String.IsNullOrEmpty(checkName))
        {
            checkName = checkName.ToLowerInvariant();

            for (int i = 0; i < surfaceTypes.Length; i++)
            {
                st = surfaceTypes[i];

                for (int ii = 0; ii < st.materialKeywords.Length; ii++)
                {
                    if (checkName.Contains(st.materialKeywords[ii].ToLowerInvariant())) //check if the material name contains the keyword
                        return true;
                }
            }
        }

        st = null;
        return false;
    }

    //You can make these public if you want access:
    private SurfaceType GetSurfaceType(Collider collider, Vector3 worldPosition, int triangleIndex = -1)
    {
        if (collider != null)
        {
            if (collider is TerrainCollider tc) //it is a terrain collider
            {
                if (TryGetTerrainSurfaceType(tc.GetComponent<Terrain>(), worldPosition, out SurfaceType st))
                    return st;
            }
            else
            {
                if (TryGetNonTerrainSurfaceType(collider, worldPosition, out SurfaceType st, triangleIndex))
                    return st;
            }
        }

        return surfaceTypes[defaultSurfaceType];
    }
    private bool TryGetTerrainSurfaceType(Terrain terrain, Vector3 worldPosition, out SurfaceType st)
    {
        var terrainIndex = GetMainTexture(terrain, worldPosition);
        var terrainTexture = terrain.terrainData.terrainLayers[terrainIndex].diffuseTexture;

        for (int i = 0; i < surfaceTypes.Length; i++)
        {
            st = surfaceTypes[i];

            for (int ii = 0; ii < st.terrainAlbedos.Length; ii++)
            {
                if (terrainTexture == st.terrainAlbedos[ii])
                {
                    return true;
                }
            }
        }

        st = null;
        return false;
    }
    private bool TryGetNonTerrainSurfaceType(Collider collider, Vector3 worldPosition, out SurfaceType st, int triangleIndex = -1)
    {
        //Finds CheckName
        string checkName = null;

        var marker = collider.GetComponent<SurfaceTypeMarker>();
        if (marker != null)
        {
            checkName = marker.reference;
        }
        else
        {
            var mr = collider.GetComponent<MeshRenderer>();

            if (mr != null)
            {
                var materials = mr.sharedMaterials;

                //Defaults to the first material. For most colliders it can't be discerned which specific material it is
                checkName = materials[0].name; 

                //The collider is a non-convex meshCollider. We can find the triangle index.
                if (triangleIndex != -1 && collider is MeshCollider mc && !mc.convex) 
                {
                    var mesh = collider.GetComponent<MeshFilter>().sharedMesh;

                    var triIndex = triangleIndex * 3;

                    for (int submeshID = mesh.subMeshCount - 1; submeshID >= 0; submeshID--)
                    {
                        int start = mesh.GetSubMesh(submeshID).indexStart;

                        if (triIndex >= start)
                        {
                            //The triangle hit is within this submesh
                            checkName = materials[submeshID].name; 
                            break;
                        }
                    }
                }
            }
        }

        //Searches using CheckName
        if (TryGetStringSurfaceType(checkName, out st))
            return true;

        //Found nothing
        st = null;
        return false;
    }
    

    //From: https://answers.unity.com/questions/456973/getting-the-texture-of-a-certain-point-on-terrain.html
    private float[] GetTextureMix(Terrain terrain, Vector3 WorldPos)
    {
        var terrainData = terrain.terrainData; //terrain = Terrain.activeTerrain;
        var terrainPos = terrain.transform.position;

        // returns an array containing the relative mix of textures
        // on the main terrain at this world position.

        // The number of values in the array will equal the number
        // of textures added to the terrain.

        // calculate which splat map cell the worldPos falls within (ignoring y)
        int mapX = (int)(((WorldPos.x - terrainPos.x) / terrainData.size.x) * terrainData.alphamapWidth);
        int mapZ = (int)(((WorldPos.z - terrainPos.z) / terrainData.size.z) * terrainData.alphamapHeight);

        // get the splat data for this cell as a 1x1xN 3d array (where N = number of textures)
        float[,,] splatmapData = terrainData.GetAlphamaps(mapX, mapZ, 1, 1);

        // extract the 3D array data to a 1D array:
        float[] cellMix = new float[splatmapData.GetUpperBound(2) + 1];

        for (int n = 0; n < cellMix.Length; n++)
        {
            cellMix[n] = splatmapData[0, 0, n];
        }
        return cellMix;
    }
    private int GetMainTexture(Terrain terrain, Vector3 WorldPos)
    {
        // returns the zero-based index of the most dominant texture
        // on the main terrain at this world position.
        float[] mix = GetTextureMix(terrain, WorldPos);

        float maxMix = 0;
        int maxIndex = 0;

        // loop through each mix value and find the maximum
        for (int n = 0; n < mix.Length; n++)
        {
            if (mix[n] > maxMix)
            {
                maxIndex = n;
                maxMix = mix[n];
            }
        }
        return maxIndex;
    }



    //Datatypes
    [System.Serializable]
    public class SurfaceType
    {
        //Fields
        [Header("______________________________________________________")]
        public string groupName = "Grassy Sound";

        [Header("Terrains")]
        public Texture2D[] terrainAlbedos;

        [Header("Mesh Renderers")]
        public string[] materialKeywords = new string[] { "Grass", "Leaves", "Hay", "Flower" };

        [Header("Sounds")]
        [ReorderableList()]
        public SoundSet[] soundSets = new SoundSet[] { new SoundSet(), new SoundSet() };


        //Methods
        public SoundSet GetSoundSet(int id = 0)
        {
            return soundSets[id];
        }


        //Datatypes
        [System.Serializable]
        public class SoundSet
        {
            //Fields
#if UNITY_EDITOR
            //[HideInInspector]
            public string autoHeader = "";
#endif

            [Header("Volume")]
            public float volume = 1;
            [Range(0, 1)]
            public float volumeVariation = 0.2f;

            [Header("Pitch")]
            public float pitch = 1;
            [Range(0, 1)]
            public float pitchVariation = 0.2f;

            [Header("Clips")]
            public Clip[] clipVariants = new Clip[1] { new Clip() };

            [Header("Friction/Rolling if wanted")]
            [Tooltip("This can be used for friction/rolling sounds, or just ignore it")]
            [Space(20)]
            public AudioClip loopSound; //(no randomization should be used for this clip)


            //Datatypes
            [System.Serializable]
            public class Clip
            {
                [Min(0)]
                public float probabilityWeight = 1; //normalized for clipVariants
                public AudioClip clip;

                public float volumeMultiplier = 1;
                public float pitchMultiplier = 1;
            }


            //Methods
            public void PlayOneShot(AudioSource audioSource, float volumeMultiplier = 1, float pitchMultiplier = 1)
            {
                var c = GetRandomClip(out float volume, out float pitch);

                if (c != null)
                {
                    //if(!source.isPlaying)
                    audioSource.pitch = pitch * pitchMultiplier;

                    audioSource.PlayOneShot(c, volume * volumeMultiplier);
                }
            }

            public AudioClip GetRandomClip(out float volume, out float pitch)
            {
                volume = GetVolume();
                pitch = GetPitch();

                var c = GetRandomClip();
                if (c != null)
                {
                    volume *= c.volumeMultiplier;
                    pitch *= c.pitchMultiplier;

                    return c.clip;
                }

                return null;
            }

            private float GetVolume()
            {
                return volume * (1 + (Random.value - 0.5f) * volumeVariation);
            }
            private float GetPitch()
            {
                return pitch * (1 + (Random.value - 0.5f) * pitchVariation);
            }
            private Clip GetRandomClip()
            {
                float totalWeight = 0;
                for (int i = 0; i < clipVariants.Length; i++)
                    totalWeight += clipVariants[i].probabilityWeight;

                float rand = Random.value * totalWeight;
                float finder = 0f;
                for (int i = 0; i < clipVariants.Length; i++)
                {
                    var cv = clipVariants[i];
                    finder += cv.probabilityWeight;
                    if (finder >= rand - 0.000000001f) //I just do that just in case of rounding errors (idk)
                        return cv;
                }

                return null;
            }
        }
    }



    //Lifecycle
#if UNITY_EDITOR
    private void OnValidate()
    {
        defaultSurfaceType = Mathf.Clamp(defaultSurfaceType, 0, surfaceTypes.Length - 1);
        autoDefaultSurfaceTypeHeader = surfaceTypes[defaultSurfaceType].groupName;


        //Grows the SoundSetNames to the maximum count
        int maxSoundSetCount = 0;
        for (int i = 0; i < surfaceTypes.Length; i++)
            maxSoundSetCount = Mathf.Max(maxSoundSetCount, surfaceTypes[i].soundSets.Length);
        if (maxSoundSetCount > soundSetNames.Length)
            System.Array.Resize(ref soundSetNames, maxSoundSetCount);


        //Applies the names to the inspector autoHeaders
        for (int i = 0; i < surfaceTypes.Length; i++)
        {
            var st = surfaceTypes[i];

            for (int ii = 0; ii < st.soundSets.Length; ii++)
            {
                var ss = st.soundSets[ii];
                ss.autoHeader = soundSetNames[ii];
            }
        }
    }
#endif
}

/*
 * 
                for (int iii = 0; iii < ss.clipVariants.Length; iii++)
                {
                    var cv = ss.clipVariants[iii];

                    if (cv.probabilityWeight == 0)
                        cv.probabilityWeight = 1;
                }

private static readonly List<int> subMeshTriangles = new List<int>(); //to avoid some amount of constant reallocation

if (mesh.isReadable)
{
    //Much slower version. I don't know if the faster version will be consistent though, because I don't know how unity does things internally, so if there are problems then see if this fixes it. In my testing the faster version works fine though:
    int[] triangles = mesh.triangles;

    var triIndex = rh.triangleIndex * 3;
    int a = triangles[triIndex + 0], b = triangles[triIndex + 1], c = triangles[triIndex + 2];

    for (int submeshID = 0; submeshID < mesh.subMeshCount; submeshID++)
    {
        subMeshTriangles.Clear();
        mesh.GetTriangles(subMeshTriangles, submeshID);

        for (int i = 0; i < subMeshTriangles.Count; i += 3)
        {
            int aa = subMeshTriangles[i + 0], bb = subMeshTriangles[i + 1], cc = subMeshTriangles[i + 2];
            if (a == aa && b == bb && c == cc)
            {
                checkName = materials[submeshID].name; //the triangle hit is within this submesh

                goto Found; //This exits the nested loop, to avoid any more comparisons (for performance)
            }
        }
    }
}

                    //Found:





#if UNITY_EDITOR
            [UnityEditor.CustomPropertyDrawer(typeof(Clip))]
            public class ClipDrawer : UnityEditor.PropertyDrawer
            {
                public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
                {
                    return UnityEditor.EditorGUIUtility.singleLineHeight;
                }

                public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
                {
                    var pw = property.FindPropertyRelative("probabilityWeight");
                    var c = property.FindPropertyRelative("clip");

                    var r = rect;
                    r.width *= 0.5f;
                    UnityEditor.EditorGUI.PropertyField(r, property, false);

                    r = rect;
                    r.width *= 0.5f;
                    UnityEditor.EditorGUI.PropertyField(r, pw);

                    r.x += r.width;
                    UnityEditor.EditorGUI.PropertyField(r, c, null as GUIContent);
                }

                private void OnEnable()
                {

                }
            }

#endif
*/
