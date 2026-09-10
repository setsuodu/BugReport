using UnityEditor;
using UnityEngine;

namespace Company.BugReport.Editor
{
    public static class BugReporterMenu
    {
        [MenuItem("Tools/BugReport/Create BugReporter GameObject")]
        public static void CreateBugReporter()
        {
            var go = new GameObject("BugReporter");
            go.AddComponent<BugReporter>();
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create BugReporter");
        }
    }
}
