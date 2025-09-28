using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

public class BubbleSort : MonoBehaviour
{
    float[] array;
    List<GameObject> mainObjects;
    public GameObject prefab;

    public enum SortType { Bubble, Insertion }
    public SortType sortType = SortType.Bubble;

    void Start()
    {
        mainObjects = new List<GameObject>();
        array = new float[30000];
        for (int i = 0; i < 30000; i++)
        {
            array[i] = (float)Random.Range(0, 1000)/100;
        }
        
        logArray();
        spawnObjs();
        updateHeights();

        // Start the selected sort in a new thread
        Thread sortingThread;
        if (sortType == SortType.Bubble)
            sortingThread = new Thread(bubbleSort);
        else
            sortingThread = new Thread(insertionSort);
        sortingThread.Start();
    }

    void Update()
    {
        //TO DO 6
        //Call ChangeHeights() in order to update our object list.
        //Since we'll be calling UnityEngine functions to retrieve and change some data,
        //we can't call this function inside a Thread
        updateHeights();

    }

    //TO DO 5
    //Create a new thread using the function "bubbleSort" and start it.
    void bubbleSort()
    {
        int i, j;
        int n = array.Length;
        bool swapped;
        for (i = 0; i < n- 1; i++)
        {
            swapped = false;
            for (j = 0; j < n - i - 1; j++)
            {
                if (array[j] > array[j + 1])
                {
                    (array[j], array[j+1]) = (array[j+1], array[j]);
                    swapped = true;
                }
            }
            if (swapped == false)
                break;
        }
        //You may debug log your Array here in case you want to. It will only be called one the bubble algorithm has finished sorting the array
    }

    void insertionSort()
    {
        int n = array.Length;
        for (int i = 1; i < n; ++i)
        {
            float key = array[i];
            int j = i - 1;

            while (j >= 0 && array[j] > key)
            {
                array[j + 1] = array[j];
                j = j - 1;
            }
            array[j + 1] = key;
        }
    }

    void logArray()
    {
        string text = "";

        //TO DO 1
        //Simply show in the console what's inside our array.
        Debug.Log("Array: ");
        for (int i = 0; i < array.Length; i++)
        {
            text += array[i] + ", ";
        }
        Debug.Log(text);
    }
    
    void spawnObjs()
    {
        //TO DO 2
        //We should be storing our objects in a list so we can access them later on.

        mainObjects.Clear();
        for (int i = 0; i < array.Length; i++)
        {
            //We have to separate the objs accordingly to their width, in which case we divide their position by 1000.
            //If you decide to make your objs wider, don't forget to up this value
            GameObject obj = Instantiate(prefab, new Vector3((float)i / 1000, 
                this.gameObject.GetComponent<Transform>().position.y, 0), Quaternion.identity);
            mainObjects.Add(obj);
        }

    }

    //TO DO 3
    //We'll just change the height of every obj in our list to match the values of the array.
    //To avoid calling this function once everything is sorted, keep track of new changes to the list.
    //If there weren't, you might as well stop calling this function

    bool updateHeights()
    {

        bool changed = false;
        for (int i = 0; i < array.Length; i++)
        {
            if (i < mainObjects.Count)
            {
                // Get current scale
                Vector3 currentScale = mainObjects[i].transform.localScale;
                
                // Create new scale with height based on array value
                Vector3 newScale = new Vector3(currentScale.x, array[i], currentScale.z);
                
                // Check if scale needs to be updated
                if (currentScale.y != newScale.y)
                {
                    mainObjects[i].transform.localScale = newScale;
                    changed = true;
                }
            }
        }
        return changed;
    }
}
