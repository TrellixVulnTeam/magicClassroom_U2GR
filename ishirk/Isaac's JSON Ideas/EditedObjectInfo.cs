﻿using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;

/*
 * This class creates a JSON object that can be serialized/deserialized easily with Unities built in capabilities
 * One of the things about JSONs that is nice is that not every field/variable has to be filled. We will take advantage of that.
 */

[System.Serializable]
public class ObjectInfo
{
    public string name; 
    public string parentName;
    public string primitiveType; 

    public Vector3 position;
    public Vector3 scale;
    public string material; //Leaving material blank won't cause any problems and just won't render a material. 

    public int numComponentsToAdd;
    public string[] componentsToAdd;
}
