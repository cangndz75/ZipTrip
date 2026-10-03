using System.Linq;
using NUnit.Framework;
using ZipTrip.Domain;

namespace ZipTrip.Tests.EditMode
{
    public sealed class DomainSmokeTests
    {
        [Test]
        public void DomainSmokeValue_CanBeCreatedAndRead()
        {
            var value = new DomainSmokeValue(42);

            Assert.That(value.Value, Is.EqualTo(42));
        }

        [Test]
        public void DomainAssembly_DoesNotReferenceUnityEngine()
        {
            Assert.That(typeof(DomainSmokeValue).Assembly.GetReferencedAssemblies()
                .Select(assembly => assembly.Name), Has.None.StartsWith("UnityEngine"));
        }
    }
}
