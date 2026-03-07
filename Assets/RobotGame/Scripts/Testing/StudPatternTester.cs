using UnityEngine;
using RobotGame.Data;
using RobotGame.Utils;
using System.Collections.Generic;

namespace RobotGame.Testing
{
    /// <summary>
    /// Tester para validar el sistema de StudPattern.
    /// Agrega este componente a un GameObject vacío y usa los botones del Inspector.
    /// </summary>
    public class StudPatternTester : MonoBehaviour
    {
        [Header("Test Results")]
        [TextArea(10, 20)]
        public string testOutput = "";
        
        [ContextMenu("Test 1: Crear Patrón Rectangular")]
        public void Test1_CreateRectangle()
        {
            var pattern = StudPattern.CreateRectangle(3, 2);
            
            testOutput = "=== Test 1: Patrón Rectangular 3x2 ===\n\n";
            testOutput += $"Studs: {pattern.Count}\n";
            testOutput += $"Bounding: {pattern.BoundingWidth}x{pattern.BoundingHeight}\n\n";
            testOutput += "Visual:\n" + pattern.ToVisualString();
            
            // Debug.Log(testOutput);
        }
        
        [ContextMenu("Test 2: Crear Patrón en L")]
        public void Test2_CreateLShape()
        {
            var pattern = new StudPattern();
            // L shape:
            // ●
            // ●
            // ● ●
            pattern.AddStud(0, 0);
            pattern.AddStud(0, 1);
            pattern.AddStud(0, 2);
            pattern.AddStud(1, 0);
            
            testOutput = "=== Test 2: Patrón en L ===\n\n";
            testOutput += $"Studs: {pattern.Count}\n";
            testOutput += $"Bounding: {pattern.BoundingWidth}x{pattern.BoundingHeight}\n\n";
            testOutput += "Visual:\n" + pattern.ToVisualString();
            
            // Debug.Log(testOutput);
        }
        
        [ContextMenu("Test 3: Rotar Patrón L")]
        public void Test3_RotateLShape()
        {
            var pattern = new StudPattern();
            pattern.AddStud(0, 0);
            pattern.AddStud(0, 1);
            pattern.AddStud(0, 2);
            pattern.AddStud(1, 0);
            
            testOutput = "=== Test 3: Rotaciones de L ===\n\n";
            
            testOutput += "Original (0°):\n" + pattern.ToVisualString() + "\n";
            testOutput += "Rotado 90°:\n" + pattern.Rotate90CW().ToVisualString() + "\n";
            testOutput += "Rotado 180°:\n" + pattern.Rotate180().ToVisualString() + "\n";
            testOutput += "Rotado 270°:\n" + pattern.Rotate270().ToVisualString() + "\n";
            
            // Debug.Log(testOutput);
        }
        
        [ContextMenu("Test 4: Validar Colocación")]
        public void Test4_ValidatePlacement()
        {
            // Head: grilla 4x3 completa
            var head = StudPattern.CreateRectangle(4, 3);
            var occupied = new HashSet<StudPosition>();
            
            // Tail: pieza 2x2
            var tail = StudPattern.CreateRectangle(2, 2);
            
            testOutput = "=== Test 4: Validar Colocación ===\n\n";
            testOutput += "Head (4x3):\n" + head.ToVisualString() + "\n";
            testOutput += "Tail (2x2):\n" + tail.ToVisualString() + "\n";
            
            // Probar posiciones
            testOutput += "Posiciones válidas:\n";
            var validPos = tail.GetValidPlacements(head, occupied);
            foreach (var pos in validPos)
            {
                testOutput += $"  ({pos.x}, {pos.y})\n";
            }
            
            testOutput += $"\nTotal: {validPos.Count} posiciones válidas\n";
            
            // Debug.Log(testOutput);
        }
        
        [ContextMenu("Test 5: Colocación con Ocupación")]
        public void Test5_PlacementWithOccupied()
        {
            // Head: grilla 4x3
            var head = StudPattern.CreateRectangle(4, 3);
            
            // Marcar algunas celdas como ocupadas
            var occupied = new HashSet<StudPosition>
            {
                new StudPosition(0, 0),
                new StudPosition(1, 0),
                new StudPosition(0, 1),
                new StudPosition(1, 1)
            };
            
            // Tail: pieza 2x2
            var tail = StudPattern.CreateRectangle(2, 2);
            
            testOutput = "=== Test 5: Colocación con Studs Ocupados ===\n\n";
            testOutput += "Head (4x3):\n" + head.ToVisualString() + "\n";
            testOutput += "Ocupados (esquina inferior izquierda 2x2):\n";
            testOutput += "X X · ·\n";
            testOutput += "X X · ·\n";
            testOutput += "· · · ·\n\n";
            
            testOutput += "Tail (2x2):\n" + tail.ToVisualString() + "\n";
            
            // Probar posiciones
            testOutput += "Posiciones válidas (evitando ocupados):\n";
            var validPos = tail.GetValidPlacements(head, occupied);
            foreach (var pos in validPos)
            {
                testOutput += $"  ({pos.x}, {pos.y})\n";
            }
            
            testOutput += $"\nTotal: {validPos.Count} posiciones válidas\n";
            testOutput += "(Debería ser 2: en (2,0) y (2,1))\n";
            
            // Debug.Log(testOutput);
        }
        
        [ContextMenu("Test 6: Head con Hueco")]
        public void Test6_HeadWithHole()
        {
            // Head en forma de U (3x3 sin centro superior)
            var head = new StudPattern();
            head.AddStud(0, 0); head.AddStud(1, 0); head.AddStud(2, 0);
            head.AddStud(0, 1); head.AddStud(1, 1); head.AddStud(2, 1);
            head.AddStud(0, 2);                     head.AddStud(2, 2);
            // Falta (1, 2) - el hueco
            
            var occupied = new HashSet<StudPosition>();
            
            // Tail: pieza 1x3 vertical
            var tailVertical = new StudPattern();
            tailVertical.AddStud(0, 0);
            tailVertical.AddStud(0, 1);
            tailVertical.AddStud(0, 2);
            
            // Tail: pieza 3x1 horizontal
            var tailHorizontal = new StudPattern();
            tailHorizontal.AddStud(0, 0);
            tailHorizontal.AddStud(1, 0);
            tailHorizontal.AddStud(2, 0);
            
            testOutput = "=== Test 6: Head con Hueco (U shape) ===\n\n";
            testOutput += "Head (U):\n" + head.ToVisualString() + "\n";
            
            testOutput += "Tail Vertical (1x3):\n" + tailVertical.ToVisualString();
            var validVertical = tailVertical.GetValidPlacements(head, occupied);
            testOutput += $"Posiciones válidas: {validVertical.Count}\n";
            foreach (var pos in validVertical)
            {
                testOutput += $"  ({pos.x}, {pos.y})\n";
            }
            testOutput += "(Solo debería poder en x=0 y x=2, ya que x=1 tiene hueco arriba)\n\n";
            
            testOutput += "Tail Horizontal (3x1):\n" + tailHorizontal.ToVisualString();
            var validHorizontal = tailHorizontal.GetValidPlacements(head, occupied);
            testOutput += $"Posiciones válidas: {validHorizontal.Count}\n";
            foreach (var pos in validHorizontal)
            {
                testOutput += $"  ({pos.x}, {pos.y})\n";
            }
            testOutput += "(Debería poder en y=0 y y=1, pero NO en y=2 por el hueco)\n";
            
            // Debug.Log(testOutput);
        }
        
        [ContextMenu("Test 7: Pieza Irregular en T")]
        public void Test7_TShape()
        {
            // T shape:
            // ● ● ●
            //   ●
            //   ●
            var tShape = new StudPattern();
            tShape.AddStud(0, 2);
            tShape.AddStud(1, 2);
            tShape.AddStud(2, 2);
            tShape.AddStud(1, 1);
            tShape.AddStud(1, 0);
            
            testOutput = "=== Test 7: Forma en T ===\n\n";
            testOutput += "T Shape:\n" + tShape.ToVisualString() + "\n";
            testOutput += $"Studs: {tShape.Count}\n";
            testOutput += $"Bounding: {tShape.BoundingWidth}x{tShape.BoundingHeight}\n\n";
            
            testOutput += "Rotaciones:\n\n";
            testOutput += "90°:\n" + tShape.Rotate90CW().ToVisualString() + "\n";
            testOutput += "180°:\n" + tShape.Rotate180().ToVisualString() + "\n";
            testOutput += "270°:\n" + tShape.Rotate270().ToVisualString() + "\n";
            
            // Debug.Log(testOutput);
        }
        
        [ContextMenu("Run All Tests")]
        public void RunAllTests()
        {
            testOutput = "";
            
            Test1_CreateRectangle();
            string t1 = testOutput;
            
            Test2_CreateLShape();
            string t2 = testOutput;
            
            Test3_RotateLShape();
            string t3 = testOutput;
            
            Test4_ValidatePlacement();
            string t4 = testOutput;
            
            Test5_PlacementWithOccupied();
            string t5 = testOutput;
            
            Test6_HeadWithHole();
            string t6 = testOutput;
            
            Test7_TShape();
            string t7 = testOutput;
            
            testOutput = t1 + "\n\n" + t2 + "\n\n" + t3 + "\n\n" + t4 + "\n\n" + t5 + "\n\n" + t6 + "\n\n" + t7;
            
            // Debug.Log("=== ALL TESTS COMPLETE ===");
        }
    }
}
