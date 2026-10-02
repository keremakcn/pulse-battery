using System;
using System.IO;
namespace Pulse {
 public static class LearningTests {
  public static int Run(){int result=V2Tests.Run();File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"v2-test-result.txt"),Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"learning-test-result.txt"),true);return result;}
 }
}
