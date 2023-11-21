using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;


public class NodePhysicsHandler : MonoBehaviour
{
    // i wont explain all this
    public NodeStructureHandler structure_handler;
    public VarHolder vars;
    public Rigidbody SelfRB;
    public Vector3 init_cords;
    public bool in_camera_physics_range = false;

    public List<GameObject> connections;
    public float previous_update_time;

    private bool useTanh;

    private LayerMask NodeLayer;

    private int[] lookup;

    // this function takes in distance, and calculates the force that should come out
    // so if the object to which the force should be applied is too far away, the force will be negative, if too close, then positive
    public Vector3 CalcTanhOfVector3(Vector3 forceVec3)
    {
            float tanhX = FastTanh(forceVec3.x);
            float tanhY = FastTanh(forceVec3.y);
            float tanhZ = FastTanh(forceVec3.z);
            return new Vector3(tanhX, tanhY, tanhZ);
    }

    private float FastTanh(float x)
    {
        //changed to a slightly faster formula, forgot my old one approaches 0 - sorry about that! Hopefully this is better
        // it best emulates the tanh function when the numerator is multiplied by 1.1, but should still be a fast and accurate approximation, since we're mainly just using it to squish values
        // desmos formula: x/\left(1+\operatorname{abs}\left(x\right)\right)
        // uses inline ternary, which should be roughly double the speed of tanh in all use-cases (I'm profiling on python bc its easier, but should be roughly the same)
        return x / (1f + ((x >= 0) ? x : -x));
    }

    // wake up settings when object is initialized
    private void Awake()
    {
        // mostly get components to be used later in the script
        structure_handler = gameObject.GetComponent<NodeStructureHandler>();
        vars = GameObject.Find("Main Camera").GetComponent<VarHolder>();
        SelfRB = gameObject.GetComponent<Rigidbody>();
        init_cords = transform.position;
        if(gameObject.name == "mould") previous_update_time = Time.time; // hacky fix for an obscure bug
        useTanh = vars.UseTanh;
        NodeLayer = LayerMask.GetMask("NodeLayer");

        //int log lookup table 
        lookup = new int[256];
        for (int i = 1; i < 256; ++i)
        {
            lookup[i] = (int)(Math.Log(i) / Math.Log(2));
        }
    }

    // since this is being used for connections, doesn't need to be too big. I've oversized it just to be on the safe side,
    // but if a node has more than 16.7 million connections, I think the logarithm will be the least of that PC's problems
    private int LogLookup(int i)
    {
        if (i >= 0x1000000) { return lookup[i >> 24] + 24; }
        else if (i >= 0x10000) { return lookup[i >> 16] + 16; }
        else if (i >= 0x100) { return lookup[i >> 8] + 8; }
        else { return lookup[i]; }  
    }

    private void Update()
    {
        useTanh = vars.UseTanh;
        // make sure that the texture of the node is looking at the camera
        // that texture can be found as one of the children in the nodes (first one in the list)
        transform.GetChild(0).transform.LookAt(vars.transform);

        // mould is an object used to clone the nodes
        // its found under the camera object
        // we dont want it to move around so dont apply physics to it
        if (gameObject.name != "mould")
        {
            // check if its the root node in a system
            // because  clones always have a parent
            if (transform.parent == null)
            {
                // if it is the root node we lock it in its initial position (check void awake()) so the graph wont fly away by accident
                transform.position = init_cords;
            }

            // if its not the root node then apply the normal physics loop
            // check if its time to update the physics (depends on the throttle)
            if (Time.time >= previous_update_time + vars.SecondsPerPhysicsUpdate)
            {
                previous_update_time = Time.time;
                PhysicsLoop();
            }
        }
    }

    private void PhysicsLoop()
    {
        if (in_camera_physics_range)
        {
            foreach (GameObject connection in connections)
            {
                // the distance that we want the current node to be held at
                float IdealDistance = 
                    LogLookup(connection.GetComponent<NodeStructureHandler>().connections.Count() + vars.MinimalChildDistance) * vars.ChildDistanceConnectionsEffect;


                // this vector is pointing towards THIS node, from the CONNECTION node
                // it is used to pull the CONNECTION node to the correct holding position
                // will be inverted if the node needs to be pushed
                Vector3 PullVector = transform.position - connection.transform.position;

                Vector3 ForceVector = CalcTanhOfVector3((PullVector.magnitude - IdealDistance) * PullVector) * vars.PhysicsForceGeneralStrength;

                connection.GetComponent<Rigidbody>().AddForce(ForceVector);
            }
        }
    }
}
