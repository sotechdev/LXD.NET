using SharpLXD;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace SharpLXDTests
{
    [TestClass]
    public class ContainerTests
    {
        public TestContext TestContext;
        private static Client s_client;


        [ClassInitialize]
        public static void Setup(TestContext context)
        {
            s_client = new Client("https://ubuntu:8443", "cert/client.p12", "");
        }

        [TestMethod]
        public void Container_ExecSimpleCommand()
        {
            ClientWebSocket ws = ((SharpLXD.Domain.Container.ContainerExecResult.ContainerExecResultWithWebSockets)(s_client.Containers[0].Exec(new[] { "uname" }).Result)).StandardError;
            string stdouterr = Task.Run(() => ws.ReadLinesAsync()).Result;

            Assert.AreEqual("Linux\r\n", stdouterr);
        }

        [TestMethod]
        public void Container_ExecNonInteractiveCommand()
        {
            ClientWebSocket ws = ((SharpLXD.Domain.Container.ContainerExecResult.ContainerExecResultWithWebSockets)(s_client.Containers[0].Exec(new[] { "uname" }, interactive: false).Result)).StandardOutput;
            string stdout = Task.Run(() => ws.ReadLinesAsync()).Result;

            Assert.AreEqual("Linux\n", stdout);
        }

        [TestMethod]
        [Ignore]
        public void Container_ExecCommandWithInput()
        {
            var res = (SharpLXD.Domain.Container.ContainerExecResult.ContainerExecResultWithWebSockets)(s_client.Containers[0].Exec(new[] { "cat" }, interactive: false).Result);

            Task.Run(() => res.StandardInput.WriteAsync("Yo\r\n")).Wait();
            string output = Task.Run(() => res.StandardOutput.ReadLinesAsync()).Result;

            Assert.AreEqual("Yo\r\n", output);
        }
    }
}
