using UnityEngine;
using RobotGame.Data;
using RobotGame.Utils;
using RobotGame.Enums;

namespace RobotGame.Testing
{
    /// <summary>
    /// Tester para validar la detección de studs desde prefabs.
    /// 
    /// CÓMO USAR:
    /// 1. Crea un prefab de prueba en Blender con la estructura:
    ///    
    ///    Head_T1-2_TestGrid
    ///    ├── Stud_0_0
    ///    ├── Stud_0_1
    ///    ├── Stud_1_0
    ///    └── Stud_1_1
    /// 
    /// 2. Arrastra el prefab al campo "testPrefab"
    /// 3. Click derecho → "Detect Studs From Prefab"
    /// </summary>
    public class StudAutoDetectorTester : MonoBehaviour
    {
        [Header("Prefab a Analizar")]
        [Tooltip("Arrastra aquí un prefab con estructura Head/Tail y Stud_X_Y")]
        public GameObject testPrefab;
        
        [Header("Test Results")]
        [TextArea(15, 30)]
        public string testOutput = "";
        
        [ContextMenu("Detect Studs From Prefab")]
        public void DetectStudsFromPrefab()
        {
            if (testPrefab == null)
            {
                testOutput = "ERROR: Asigna un prefab primero.";
                // Debug.LogWarning(testOutput);
                return;
            }
            
            testOutput = $"=== Analizando: {testPrefab.name} ===\n\n";
            
            // Detectar Heads
            var heads = StudAutoDetector.DetectHeads(testPrefab);
            testOutput += $"HEADS ENCONTRADOS: {heads.Count}\n";
            testOutput += "─────────────────────────────\n";
            
            foreach (var head in heads)
            {
                testOutput += $"\n  Transform: {head.transformName}\n";
                testOutput += $"  Nombre: {head.name}\n";
                testOutput += $"  Tier: {head.tierInfo}\n";
                testOutput += $"  Studs: {head.pattern.Count}\n";
                testOutput += $"  Bounding: {head.pattern.BoundingWidth}x{head.pattern.BoundingHeight}\n";
                testOutput += $"  Patrón:\n";
                
                // Indentar el patrón visual
                string[] lines = head.pattern.ToVisualString().Split('\n');
                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        testOutput += $"    {line}\n";
                }
            }
            
            // Detectar Tails
            var tails = StudAutoDetector.DetectTails(testPrefab);
            testOutput += $"\n\nTAILS ENCONTRADOS: {tails.Count}\n";
            testOutput += "─────────────────────────────\n";
            
            foreach (var tail in tails)
            {
                testOutput += $"\n  Transform: {tail.transformName}\n";
                testOutput += $"  Nombre: {tail.name}\n";
                testOutput += $"  Tier: {tail.tierInfo}\n";
                testOutput += $"  Studs: {tail.pattern.Count}\n";
                testOutput += $"  Bounding: {tail.pattern.BoundingWidth}x{tail.pattern.BoundingHeight}\n";
                testOutput += $"  Patrón:\n";
                
                string[] lines = tail.pattern.ToVisualString().Split('\n');
                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        testOutput += $"    {line}\n";
                }
            }
            
            if (heads.Count == 0 && tails.Count == 0)
            {
                testOutput += "\n⚠ No se encontraron grillas.\n";
                testOutput += "Verifica que el prefab tenga la nomenclatura correcta:\n";
                testOutput += "  - Head_T1-2_nombre (contenedor)\n";
                testOutput += "  - Tail_T1-2_nombre (contenedor)\n";
                testOutput += "  - Stud_X_Y (hijos del contenedor)\n";
            }
            
            // Debug.Log(testOutput);
        }
        
        [ContextMenu("Analyze All Children")]
        public void AnalyzeAllChildren()
        {
            if (testPrefab == null)
            {
                testOutput = "ERROR: Asigna un prefab primero.";
                // Debug.LogWarning(testOutput);
                return;
            }
            
            testOutput = $"=== Análisis Completo: {testPrefab.name} ===\n\n";
            testOutput += "Todos los objetos y su clasificación:\n";
            testOutput += "─────────────────────────────────────\n\n";
            
            AnalyzeTransform(testPrefab.transform, 0);
            
            // Debug.Log(testOutput);
        }
        
        private void AnalyzeTransform(Transform t, int depth)
        {
            string indent = new string(' ', depth * 2);
            string type = StudAutoDetector.GetObjectType(t.name);
            string typeLabel = "";
            
            switch (type)
            {
                case "Head":
                    if (StudAutoDetector.TryParseHead(t.name, out TierInfo headTier, out string headName))
                    {
                        typeLabel = $" [HEAD T{headTier}]";
                    }
                    break;
                case "Tail":
                    if (StudAutoDetector.TryParseTail(t.name, out TierInfo tailTier, out string tailName))
                    {
                        typeLabel = $" [TAIL T{tailTier}]";
                    }
                    break;
                case "Stud":
                    if (StudAutoDetector.TryParseStud(t.name, out int x, out int y))
                    {
                        typeLabel = $" [STUD ({x},{y})]";
                    }
                    break;
                default:
                    typeLabel = " (ignorado)";
                    break;
            }
            
            testOutput += $"{indent}{t.name}{typeLabel}\n";
            
            foreach (Transform child in t)
            {
                AnalyzeTransform(child, depth + 1);
            }
        }
        
        [ContextMenu("Test Nomenclature Parsing")]
        public void TestNomenclatureParsing()
        {
            testOutput = "=== Test de Parsing de Nomenclatura ===\n\n";
            
            // Test Heads
            string[] headTests = {
                "Head_T1-2_TorsoMain",
                "Head_T2-3_ShoulderLeft",
                "Head_T1_SimpleName",
                "Head_T3-1_Test",
                "InvalidHead",
                "Head_TorsoMain"
            };
            
            testOutput += "HEADS:\n";
            foreach (var test in headTests)
            {
                bool success = StudAutoDetector.TryParseHead(test, out TierInfo tier, out string name);
                testOutput += $"  {test}\n";
                testOutput += $"    → {(success ? $"✓ Tier={tier}, Name={name}" : "✗ No válido")}\n";
            }
            
            // Test Tails
            string[] tailTests = {
                "Tail_T1-2_ChestPlate",
                "Tail_T2-1_ArmGuard",
                "Tail_T1_SimpleArmor",
                "InvalidTail",
                "Tail_Armor"
            };
            
            testOutput += "\nTAILS:\n";
            foreach (var test in tailTests)
            {
                bool success = StudAutoDetector.TryParseTail(test, out TierInfo tier, out string name);
                testOutput += $"  {test}\n";
                testOutput += $"    → {(success ? $"✓ Tier={tier}, Name={name}" : "✗ No válido")}\n";
            }
            
            // Test Studs
            string[] studTests = {
                "Stud_0_0",
                "Stud_1_2",
                "Stud_10_15",
                "Stud_0_0_Extra",
                "NotAStud",
                "Stud_A_B"
            };
            
            testOutput += "\nSTUDS:\n";
            foreach (var test in studTests)
            {
                bool success = StudAutoDetector.TryParseStud(test, out int x, out int y);
                testOutput += $"  {test}\n";
                testOutput += $"    → {(success ? $"✓ Position=({x},{y})" : "✗ No válido")}\n";
            }
            
            // Debug.Log(testOutput);
        }
        
        [ContextMenu("Create Test Hierarchy In Scene")]
        public void CreateTestHierarchyInScene()
        {
            // Crear un Head de prueba con studs
            GameObject headObj = new GameObject("Head_T1-2_TestGrid");
            headObj.transform.position = Vector3.zero;
            
            // Crear studs en forma de L
            CreateEmptyChild(headObj, "Stud_0_0", new Vector3(0, 0, 0));
            CreateEmptyChild(headObj, "Stud_0_1", new Vector3(0, 0.1f, 0));
            CreateEmptyChild(headObj, "Stud_0_2", new Vector3(0, 0.2f, 0));
            CreateEmptyChild(headObj, "Stud_1_0", new Vector3(0.1f, 0, 0));
            
            // Crear un Tail de prueba
            GameObject tailObj = new GameObject("Tail_T1-2_TestPiece");
            tailObj.transform.position = new Vector3(2, 0, 0);
            
            // Crear studs 2x2
            CreateEmptyChild(tailObj, "Stud_0_0", new Vector3(0, 0, 0));
            CreateEmptyChild(tailObj, "Stud_0_1", new Vector3(0, 0.1f, 0));
            CreateEmptyChild(tailObj, "Stud_1_0", new Vector3(0.1f, 0, 0));
            CreateEmptyChild(tailObj, "Stud_1_1", new Vector3(0.1f, 0.1f, 0));
            
            testOutput = "Jerarquía de prueba creada en la escena.\n\n";
            testOutput += "Head_T1-2_TestGrid (forma L):\n";
            testOutput += "  ● · \n";
            testOutput += "  ● · \n";
            testOutput += "  ● ● \n\n";
            testOutput += "Tail_T1-2_TestPiece (2x2):\n";
            testOutput += "  ● ● \n";
            testOutput += "  ● ● \n\n";
            testOutput += "Ahora puedes:\n";
            testOutput += "1. Crear un prefab desde Head_T1-2_TestGrid\n";
            testOutput += "2. Arrastrarlo al campo testPrefab\n";
            testOutput += "3. Ejecutar 'Detect Studs From Prefab'\n";
            
            // Debug.Log(testOutput);
        }
        
        private void CreateEmptyChild(GameObject parent, string name, Vector3 localPos)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent.transform);
            child.transform.localPosition = localPos;
        }
    }
}
