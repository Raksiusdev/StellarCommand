using UnityEngine;

namespace StellarCommand.Editor
{
    /// <summary>
    /// The parent of every mirrored copy used for the floor reflection: a transform at the origin scaled
    /// (1, -1, 1). Children placed at a source object's world pose appear as its reflection in the floor
    /// plane y = 0. Each builder owns a named container under it and rebuilds only its own.
    /// </summary>
    public static class FloorReflectionRoot
    {
        public const string RootName = "Floor Reflection";

        public static Transform GetRoot()
        {
            var go = GameObject.Find(RootName);
            if (go == null) go = new GameObject(RootName);

            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = new Vector3(1f, -1f, 1f);
            return go.transform;
        }

        /// <summary>Creates (replacing any previous one) an empty identity container under the mirror root.</summary>
        public static Transform CreateContainer(string name)
        {
            var root = GetRoot();

            var old = root.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var container = new GameObject(name).transform;
            container.SetParent(root, false);
            container.localPosition = Vector3.zero;
            container.localRotation = Quaternion.identity;
            container.localScale = Vector3.one;
            return container;
        }
    }
}
