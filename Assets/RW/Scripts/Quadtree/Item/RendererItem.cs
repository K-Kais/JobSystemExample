using UnityEngine;
using System.Collections;
using System.Threading.Tasks;

public class RendererItem : GameObjectItem
{
    [SerializeField] private Renderer renderer;
    public override Bounds GetBounds() => renderer.bounds;

    private IEnumerator Start()
    {
        base.Init();
        yield return new WaitForEndOfFrame();
   
    }
    private void Update()
    {
        //ar all = Root.Find(new Bounds(transform.position, new Vector3(1f, 1f)));

        //Debug.Log(all.Count + gameObject.name);
        //foreach (var item in all)
        //{
        //    Debug.Log(item);
        //}
    }
    //private async void Start()
    //{
    //    base.Init();
    //    await Task.Yield();
    //    var Bounds = new Bounds(transform.position, new Vector3(2f, 2f));
    //    var all = await Root.FindAsync(Bounds);
    //    Debug.Log(all.Count + gameObject.name);
    //    Debug.DrawLine(Bounds.min, new Vector3(Bounds.max.x, Bounds.min.y, Bounds.min.z), Color.red, 1.0f);
    //    Debug.DrawLine(Bounds.min, new Vector3(Bounds.min.x, Bounds.max.y, Bounds.min.z), Color.red, 1.0f);
    //    Debug.DrawLine(Bounds.max, new Vector3(Bounds.max.x, Bounds.min.y, Bounds.min.z), Color.red, 1.0f);
    //    Debug.DrawLine(Bounds.max, new Vector3(Bounds.min.x, Bounds.max.y, Bounds.min.z), Color.red, 1.0f);
    //}
}