using UnityEditor;
using UnityEngine;
using PizzaVR.Customers;

public static class CustomerPrefabBuilder
{
    public const string PrefabPath = "Assets/Prefabs/Customer.prefab";

    [MenuItem("Tools/Pizza VR/Build Customer Prefab")]
    public static GameObject BuildPrefab()
    {
        var root = new GameObject("Customer");
        var customer = root.AddComponent<Customer>();

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform);
        body.transform.localPosition = new Vector3(0, 0.8f, 0);
        body.transform.localScale = new Vector3(0.5f, 1.6f, 0.5f);
        SetColor(body, new Color(0.5f, 0.55f, 0.6f));
        body.AddComponent<CustomerBody>().customer = customer;

        var ticket = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ticket.name = "OrderTicket";
        ticket.transform.SetParent(root.transform);
        ticket.transform.localPosition = new Vector3(0, 1.9f, 0);
        ticket.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
        SetColor(ticket, Color.white);

        var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plate.name = "Plate";
        plate.transform.SetParent(root.transform);
        plate.transform.localPosition = new Vector3(0, 0.9f, 0.35f);
        plate.transform.localScale = new Vector3(0.4f, 0.05f, 0.4f);
        SetColor(plate, new Color(0.85f, 0.85f, 0.85f));
        var plateCollider = plate.GetComponent<Collider>();
        plateCollider.isTrigger = true;
        plate.AddComponent<CustomerPlate>().customer = customer;

        var so = new SerializedObject(customer);
        so.FindProperty("orderTicketRenderer").objectReferenceValue = ticket.GetComponent<Renderer>();
        so.FindProperty("bodyRenderer").objectReferenceValue = body.GetComponent<Renderer>();
        so.ApplyModifiedPropertiesWithoutUndo();

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        Debug.Log("CustomerPrefabBuilder: Customer prefab created at " + PrefabPath);
        return prefab;
    }

    // Always rebuilds - this prefab is code-generated (placeholder cubes), not hand-tweaked,
    // so regenerating on every scene build keeps it in sync with Customer.cs's fields.
    public static GameObject EnsurePrefab()
    {
        return BuildPrefab();
    }

    static void SetColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        renderer.sharedMaterial = mat;
    }
}
