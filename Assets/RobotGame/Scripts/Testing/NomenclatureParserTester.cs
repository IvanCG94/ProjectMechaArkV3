using UnityEngine;
using RobotGame.Utils;
using RobotGame.Enums;

namespace RobotGame.Testing
{
    /// <summary>
    /// Script de prueba para verificar que el parser de nomenclatura funciona correctamente.
    /// Agregar a un GameObject y presionar T para ejecutar las pruebas.
    /// </summary>
    public class NomenclatureParserTester : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.T))
            {
                RunTests();
            }
        }
        
        private void RunTests()
        {
            Debug.Log("=== NOMENCLATURE PARSER TESTS ===\n");
            
            // Pruebas de formato legacy (compatibilidad)
            TestParse("Head_2x2_SN_chest", true, 2, 2, 0, EdgeFlags.None, FullType.None, "chest");
            TestParse("Head_2x2_S1_chest", true, 2, 2, 1, EdgeFlags.None, FullType.None, "chest");
            TestParse("Tail_1x4_SN_plate", false, 1, 4, 0, EdgeFlags.None, FullType.None, "plate");
            TestParse("Tail_2x2_S1_armor", false, 2, 2, 1, EdgeFlags.None, FullType.None, "armor");
            
            // Pruebas con bordes simples
            TestParse("Head_2x2_S1_L_chest", true, 2, 2, 1, EdgeFlags.L, FullType.None, "chest");
            TestParse("Head_2x2_S1_R_chest", true, 2, 2, 1, EdgeFlags.R, FullType.None, "chest");
            TestParse("Head_2x2_S1_T_chest", true, 2, 2, 1, EdgeFlags.T, FullType.None, "chest");
            TestParse("Head_2x2_S1_B_chest", true, 2, 2, 1, EdgeFlags.B, FullType.None, "chest");
            
            // Pruebas con múltiples bordes
            TestParse("Head_2x4_S1_LR_torso", true, 2, 4, 1, EdgeFlags.LR, FullType.None, "torso");
            TestParse("Head_2x4_S1_TB_torso", true, 2, 4, 1, EdgeFlags.TB, FullType.None, "torso");
            TestParse("Head_3x3_S1_LT_corner", true, 3, 3, 1, EdgeFlags.LT, FullType.None, "corner");
            TestParse("Head_3x3_S2_LRTB_open", true, 3, 3, 2, EdgeFlags.LRTB, FullType.None, "open");
            
            // Pruebas con 3 bordes
            TestParse("Head_2x2_S1_LRT_threeside", true, 2, 2, 1, EdgeFlags.LRT, FullType.None, "threeside");
            TestParse("Head_2x2_S1_LRB_threeside", true, 2, 2, 1, EdgeFlags.LRB, FullType.None, "threeside");
            
            // Pruebas con Full type
            TestParse("Head_2x2_S1FH_tube", true, 2, 2, 1, EdgeFlags.None, FullType.FH, "tube");
            TestParse("Head_2x2_S1FV_tube", true, 2, 2, 1, EdgeFlags.None, FullType.FV, "tube");
            
            // Pruebas Tail con bordes
            TestParse("Tail_1x2_S1_R_wing", false, 1, 2, 1, EdgeFlags.R, FullType.None, "wing");
            TestParse("Tail_1x2_S1_LR_doubleWing", false, 1, 2, 1, EdgeFlags.LR, FullType.None, "doubleWing");
            
            // Pruebas Tail con Full (ahora debe ser FH o FV)
            TestParse("Tail_1x2_S1FH_tube", false, 1, 2, 1, EdgeFlags.None, FullType.FH, "tube");
            TestParse("Tail_1x2_S1FV_tube", false, 1, 2, 1, EdgeFlags.None, FullType.FV, "tube");
            
            Debug.Log("\n=== SURROUNDING LEVEL COMPATIBILITY TESTS ===\n");
            
            // Pruebas de compatibilidad de nivel
            TestCompatibility("S1", "S1", true, "Same level");
            TestCompatibility("S2", "S1", true, "Head higher level");
            TestCompatibility("S1", "S2", false, "Tail higher level");
            
            // Pruebas de compatibilidad de bordes
            TestCompatibility("S1_LR", "S1_L", true, "Head has both, Tail needs L");
            TestCompatibility("S1_L", "S1_LR", false, "Head only has L, Tail needs LR");
            TestCompatibility("S1_LRTB", "S1_LT", true, "Head has all, Tail needs corner");
            TestCompatibility("S2_LRTB", "S1_LRTB", true, "LRTB with level hierarchy");
            TestCompatibility("S1_LRTB", "S2_LRTB", false, "LRTB but Tail level too high");
            
            // Pruebas de compatibilidad Full
            Debug.Log("\n--- Full Type Compatibility ---\n");
            TestCompatibility("S1FH", "S1FH", true, "FH accepts FH");
            TestCompatibility("S1FV", "S1FV", true, "FV accepts FV");
            TestCompatibility("S1FH", "S1FV", false, "FH does not accept FV");
            TestCompatibility("S1FV", "S1FH", false, "FV does not accept FH");
            
            // Pruebas Full con bordes
            Debug.Log("\n--- Full with Edge Compatibility ---\n");
            TestCompatibility("S1FH", "S1_LR", true, "FH accepts LR");
            TestCompatibility("S1FH", "S1_L", true, "FH accepts L");
            TestCompatibility("S1FH", "S1_R", true, "FH accepts R");
            TestCompatibility("S1FH", "S1_TB", false, "FH does not accept TB");
            TestCompatibility("S1FH", "S1_T", false, "FH does not accept T");
            TestCompatibility("S1FH", "S1_B", false, "FH does not accept B");
            
            TestCompatibility("S1FV", "S1_TB", true, "FV accepts TB");
            TestCompatibility("S1FV", "S1_T", true, "FV accepts T");
            TestCompatibility("S1FV", "S1_B", true, "FV accepts B");
            TestCompatibility("S1FV", "S1_LR", false, "FV does not accept LR");
            TestCompatibility("S1FV", "S1_L", false, "FV does not accept L");
            TestCompatibility("S1FV", "S1_R", false, "FV does not accept R");
            
            // Prueba: Head no Full no acepta Tail Full
            Debug.Log("\n--- Non-Full Head with Full Tail ---\n");
            TestCompatibility("S1_LR", "S1FH", false, "Non-Full Head does not accept Full Tail");
            TestCompatibility("S1_LRTB", "S1FH", false, "LRTB Head does not accept Full Tail");
            
            Debug.Log("\n=== TESTS COMPLETE ===");
        }
        
        private void TestParse(string input, bool expectedIsHead, int expectedSizeX, int expectedSizeY, 
            int expectedLevel, EdgeFlags expectedEdges, FullType expectedFullType, string expectedName)
        {
            bool success = NomenclatureParser.TryParse(input, out var info);
            
            if (!success)
            {
                Debug.LogError($"FAIL: Could not parse '{input}'");
                return;
            }
            
            bool pass = true;
            string errors = "";
            
            if (info.isHead != expectedIsHead)
            {
                pass = false;
                errors += $" isHead={info.isHead}(expected {expectedIsHead})";
            }
            if (info.sizeX != expectedSizeX)
            {
                pass = false;
                errors += $" sizeX={info.sizeX}(expected {expectedSizeX})";
            }
            if (info.sizeY != expectedSizeY)
            {
                pass = false;
                errors += $" sizeY={info.sizeY}(expected {expectedSizeY})";
            }
            if (info.surrounding.level != expectedLevel)
            {
                pass = false;
                errors += $" level={info.surrounding.level}(expected {expectedLevel})";
            }
            if (info.surrounding.edges != expectedEdges)
            {
                pass = false;
                errors += $" edges={info.surrounding.edges}(expected {expectedEdges})";
            }
            if (info.surrounding.fullType != expectedFullType)
            {
                pass = false;
                errors += $" fullType={info.surrounding.fullType}(expected {expectedFullType})";
            }
            if (info.gridName != expectedName)
            {
                pass = false;
                errors += $" name={info.gridName}(expected {expectedName})";
            }
            
            if (pass)
            {
                Debug.Log($"<color=green>PASS:</color> {input}");
            }
            else
            {
                Debug.LogError($"FAIL: {input} -{errors}");
            }
        }
        
        private void TestCompatibility(string headSurrounding, string tailSurrounding, bool expectedResult, string description)
        {
            var head = SurroundingLevel.Parse(headSurrounding);
            var tail = SurroundingLevel.Parse(tailSurrounding);
            
            bool result = head.CanAccept(tail);
            
            if (result == expectedResult)
            {
                Debug.Log($"<color=green>PASS:</color> Head({headSurrounding}) + Tail({tailSurrounding}) = {result} - {description}");
            }
            else
            {
                Debug.LogError($"FAIL: Head({headSurrounding}) + Tail({tailSurrounding}) = {result} (expected {expectedResult}) - {description}");
            }
        }
    }
}
