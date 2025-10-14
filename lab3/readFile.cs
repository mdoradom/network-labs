using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class readFile : MonoBehaviour
{
 void deserialize()
    {
        MemoryStream ms = new MemoryStream();
        using (FileStream fs = File.OpenRead(Application.dataPath + ""))
        {
            fs.CopyTo(ms);
        }
    }
}
