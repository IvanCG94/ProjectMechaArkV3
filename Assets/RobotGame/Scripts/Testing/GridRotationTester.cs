using UnityEngine;
using RobotGame.Utils;
using RobotGame.Enums;
using RobotGame.Data;

namespace RobotGame.Testing
{
    /// <summary>
    /// Script de prueba para verificar que el sistema de rotación funciona correctamente.
    /// Agregar a un GameObject y presionar Y para ejecutar las pruebas.
    /// </summary>
    public class GridRotationTester : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Y))
            {
                RunTests();
            }
        }
        
        private void RunTests()
        {
            Debug.Log("=== GRID ROTATION TESTS ===\n");
            
            // Pruebas de rotación de bordes simples
            Debug.Log("--- Single Edge Rotation ---\n");
            TestEdgeRotation(EdgeFlags.L, GridRotation.Rotation.Deg0, EdgeFlags.L);
            TestEdgeRotation(EdgeFlags.L, GridRotation.Rotation.Deg90, EdgeFlags.T);
            TestEdgeRotation(EdgeFlags.L, GridRotation.Rotation.Deg180, EdgeFlags.R);
            TestEdgeRotation(EdgeFlags.L, GridRotation.Rotation.Deg270, EdgeFlags.B);
            
            TestEdgeRotation(EdgeFlags.R, GridRotation.Rotation.Deg90, EdgeFlags.B);
            TestEdgeRotation(EdgeFlags.T, GridRotation.Rotation.Deg90, EdgeFlags.R);
            TestEdgeRotation(EdgeFlags.B, GridRotation.Rotation.Deg90, EdgeFlags.L);
            
            // Pruebas de rotación de bordes dobles
            Debug.Log("\n--- Double Edge Rotation ---\n");
            TestEdgeRotation(EdgeFlags.LR, GridRotation.Rotation.Deg0, EdgeFlags.LR);
            TestEdgeRotation(EdgeFlags.LR, GridRotation.Rotation.Deg90, EdgeFlags.TB);
            TestEdgeRotation(EdgeFlags.LR, GridRotation.Rotation.Deg180, EdgeFlags.LR);
            TestEdgeRotation(EdgeFlags.LR, GridRotation.Rotation.Deg270, EdgeFlags.TB);
            
            TestEdgeRotation(EdgeFlags.TB, GridRotation.Rotation.Deg90, EdgeFlags.LR);
            TestEdgeRotation(EdgeFlags.LT, GridRotation.Rotation.Deg90, EdgeFlags.RT);
            TestEdgeRotation(EdgeFlags.LT, GridRotation.Rotation.Deg180, EdgeFlags.RB);
            TestEdgeRotation(EdgeFlags.LT, GridRotation.Rotation.Deg270, EdgeFlags.LB);
            
            // Pruebas de rotación de bordes triples
            Debug.Log("\n--- Triple Edge Rotation ---\n");
            TestEdgeRotation(EdgeFlags.LRT, GridRotation.Rotation.Deg90, EdgeFlags.RTB);
            TestEdgeRotation(EdgeFlags.LRT, GridRotation.Rotation.Deg180, EdgeFlags.LRB);
            TestEdgeRotation(EdgeFlags.LRT, GridRotation.Rotation.Deg270, EdgeFlags.LTB);
            
            // LRTB no cambia
            Debug.Log("\n--- LRTB Rotation (should not change) ---\n");
            TestEdgeRotation(EdgeFlags.LRTB, GridRotation.Rotation.Deg0, EdgeFlags.LRTB);
            TestEdgeRotation(EdgeFlags.LRTB, GridRotation.Rotation.Deg90, EdgeFlags.LRTB);
            TestEdgeRotation(EdgeFlags.LRTB, GridRotation.Rotation.Deg180, EdgeFlags.LRTB);
            TestEdgeRotation(EdgeFlags.LRTB, GridRotation.Rotation.Deg270, EdgeFlags.LRTB);
            
            // Pruebas de rotación de tamaño
            Debug.Log("\n--- Size Rotation ---\n");
            TestSizeRotation(1, 4, GridRotation.Rotation.Deg0, 1, 4);
            TestSizeRotation(1, 4, GridRotation.Rotation.Deg90, 4, 1);
            TestSizeRotation(1, 4, GridRotation.Rotation.Deg180, 1, 4);
            TestSizeRotation(1, 4, GridRotation.Rotation.Deg270, 4, 1);
            
            TestSizeRotation(2, 2, GridRotation.Rotation.Deg90, 2, 2); // Cuadrado no cambia
            
            // Pruebas de rotación de FullType
            Debug.Log("\n--- FullType Rotation ---\n");
            TestFullTypeRotation(FullType.FH, GridRotation.Rotation.Deg0, FullType.FH);
            TestFullTypeRotation(FullType.FH, GridRotation.Rotation.Deg90, FullType.FV);
            TestFullTypeRotation(FullType.FH, GridRotation.Rotation.Deg180, FullType.FH);
            TestFullTypeRotation(FullType.FH, GridRotation.Rotation.Deg270, FullType.FV);
            
            TestFullTypeRotation(FullType.FV, GridRotation.Rotation.Deg90, FullType.FH);
            TestFullTypeRotation(FullType.FV, GridRotation.Rotation.Deg270, FullType.FH);
            
            // Pruebas de GridInfo completo
            Debug.Log("\n--- Full GridInfo Rotation ---\n");
            TestGridInfoRotation("Tail_1x4_S1_R_test", GridRotation.Rotation.Deg90, 4, 1, EdgeFlags.B);
            TestGridInfoRotation("Tail_1x4_S1_R_test", GridRotation.Rotation.Deg180, 1, 4, EdgeFlags.L);
            TestGridInfoRotation("Tail_1x4_S1_R_test", GridRotation.Rotation.Deg270, 4, 1, EdgeFlags.T);
            
            TestGridInfoRotation("Tail_2x2_S1_LR_test", GridRotation.Rotation.Deg90, 2, 2, EdgeFlags.TB);
            
            TestGridInfoRotation("Tail_1x2_S1FH_test", GridRotation.Rotation.Deg90, 2, 1, FullType.FV);
            
            // Pruebas de rotaciones válidas
            Debug.Log("\n--- Valid Rotations Test ---\n");
            TestValidRotations("Tail_1x4_SN_test", "Head_2x4_SN_grid", new[] { 
                GridRotation.Rotation.Deg0, GridRotation.Rotation.Deg180 
            });
            TestValidRotations("Tail_2x2_SN_test", "Head_2x2_SN_grid", new[] { 
                GridRotation.Rotation.Deg0, GridRotation.Rotation.Deg90, 
                GridRotation.Rotation.Deg180, GridRotation.Rotation.Deg270 
            });
            TestValidRotations("Tail_1x2_S1_R_test", "Head_2x2_S1_R_grid", new[] { 
                GridRotation.Rotation.Deg0 
            });
            TestValidRotations("Tail_1x2_S1_R_test", "Head_2x2_S1_LR_grid", new[] { 
                GridRotation.Rotation.Deg0, GridRotation.Rotation.Deg180 
            });
            
            Debug.Log("\n=== TESTS COMPLETE ===");
        }
        
        private void TestEdgeRotation(EdgeFlags original, GridRotation.Rotation rotation, EdgeFlags expected)
        {
            EdgeFlags result = GridRotation.RotateEdges(original, rotation);
            
            string originalStr = SurroundingLevel.EdgesToString(original);
            string expectedStr = SurroundingLevel.EdgesToString(expected);
            string resultStr = SurroundingLevel.EdgesToString(result);
            
            if (result == expected)
            {
                Debug.Log($"<color=green>PASS:</color> {originalStr} rotated {rotation} = {resultStr}");
            }
            else
            {
                Debug.LogError($"FAIL: {originalStr} rotated {rotation} = {resultStr} (expected {expectedStr})");
            }
        }
        
        private void TestSizeRotation(int sizeX, int sizeY, GridRotation.Rotation rotation, int expectedX, int expectedY)
        {
            Vector2Int result = GridRotation.RotateSize(sizeX, sizeY, rotation);
            
            if (result.x == expectedX && result.y == expectedY)
            {
                Debug.Log($"<color=green>PASS:</color> {sizeX}x{sizeY} rotated {rotation} = {result.x}x{result.y}");
            }
            else
            {
                Debug.LogError($"FAIL: {sizeX}x{sizeY} rotated {rotation} = {result.x}x{result.y} (expected {expectedX}x{expectedY})");
            }
        }
        
        private void TestFullTypeRotation(FullType original, GridRotation.Rotation rotation, FullType expected)
        {
            FullType result = GridRotation.RotateFullType(original, rotation);
            
            if (result == expected)
            {
                Debug.Log($"<color=green>PASS:</color> {original} rotated {rotation} = {result}");
            }
            else
            {
                Debug.LogError($"FAIL: {original} rotated {rotation} = {result} (expected {expected})");
            }
        }
        
        private void TestGridInfoRotation(string tailName, GridRotation.Rotation rotation, int expectedSizeX, int expectedSizeY, EdgeFlags expectedEdges)
        {
            if (!NomenclatureParser.TryParse(tailName, out GridInfo tailInfo))
            {
                Debug.LogError($"FAIL: Could not parse '{tailName}'");
                return;
            }
            
            GridInfo rotated = GridRotation.RotateGridInfo(tailInfo, rotation);
            
            bool pass = rotated.sizeX == expectedSizeX && 
                        rotated.sizeY == expectedSizeY && 
                        rotated.surrounding.edges == expectedEdges;
            
            if (pass)
            {
                Debug.Log($"<color=green>PASS:</color> {tailName} rotated {rotation} = {rotated.sizeX}x{rotated.sizeY} {SurroundingLevel.EdgesToString(rotated.surrounding.edges)}");
            }
            else
            {
                Debug.LogError($"FAIL: {tailName} rotated {rotation} = {rotated.sizeX}x{rotated.sizeY} {SurroundingLevel.EdgesToString(rotated.surrounding.edges)} " +
                    $"(expected {expectedSizeX}x{expectedSizeY} {SurroundingLevel.EdgesToString(expectedEdges)})");
            }
        }
        
        private void TestGridInfoRotation(string tailName, GridRotation.Rotation rotation, int expectedSizeX, int expectedSizeY, FullType expectedFullType)
        {
            if (!NomenclatureParser.TryParse(tailName, out GridInfo tailInfo))
            {
                Debug.LogError($"FAIL: Could not parse '{tailName}'");
                return;
            }
            
            GridInfo rotated = GridRotation.RotateGridInfo(tailInfo, rotation);
            
            bool pass = rotated.sizeX == expectedSizeX && 
                        rotated.sizeY == expectedSizeY && 
                        rotated.surrounding.fullType == expectedFullType;
            
            if (pass)
            {
                Debug.Log($"<color=green>PASS:</color> {tailName} rotated {rotation} = {rotated.sizeX}x{rotated.sizeY} {rotated.surrounding.fullType}");
            }
            else
            {
                Debug.LogError($"FAIL: {tailName} rotated {rotation} = {rotated.sizeX}x{rotated.sizeY} {rotated.surrounding.fullType} " +
                    $"(expected {expectedSizeX}x{expectedSizeY} {expectedFullType})");
            }
        }
        
        private void TestValidRotations(string tailName, string headName, GridRotation.Rotation[] expectedRotations)
        {
            if (!NomenclatureParser.TryParse(tailName, out GridInfo tailInfo))
            {
                Debug.LogError($"FAIL: Could not parse '{tailName}'");
                return;
            }
            
            if (!NomenclatureParser.TryParse(headName, out GridInfo headInfo))
            {
                Debug.LogError($"FAIL: Could not parse '{headName}'");
                return;
            }
            
            var validRotations = GridRotation.GetValidRotations(tailInfo, headInfo);
            
            bool pass = validRotations.Length == expectedRotations.Length;
            if (pass)
            {
                for (int i = 0; i < expectedRotations.Length; i++)
                {
                    bool found = false;
                    foreach (var rot in validRotations)
                    {
                        if (rot == expectedRotations[i])
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        pass = false;
                        break;
                    }
                }
            }
            
            string validStr = string.Join(", ", System.Array.ConvertAll(validRotations, r => r.ToString()));
            string expectedStr = string.Join(", ", System.Array.ConvertAll(expectedRotations, r => r.ToString()));
            
            if (pass)
            {
                Debug.Log($"<color=green>PASS:</color> {tailName} in {headName} valid rotations: [{validStr}]");
            }
            else
            {
                Debug.LogError($"FAIL: {tailName} in {headName} valid rotations: [{validStr}] (expected [{expectedStr}])");
            }
        }
    }
}
