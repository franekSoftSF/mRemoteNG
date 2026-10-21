using mRemoteNG.Connection;
using NUnit.Framework;
using System.ComponentModel;

namespace mRemoteNGTests.Connection
{
    /// <summary>
    /// The property grid reaches the property through TypeDescriptor, not directly, so
    /// that path is checked separately from the plain property access.
    /// </summary>
    public class SmartcardCertificateGridTests
    {
        private const string Identifier = "CAPI:1AC8798C0F78BF6DA810E712EBFB95B8C7D539D5";

        [Test]
        public void PropertyIsVisibleToTheGrid()
        {
            ConnectionInfo connection = new();

            PropertyDescriptor descriptor = TypeDescriptor.GetProperties(connection)["SmartcardCertificate"];

            Assert.That(descriptor, Is.Not.Null);
        }

        [Test]
        public void ValueSurvivesARoundTripThroughTheDescriptor()
        {
            ConnectionInfo connection = new();
            PropertyDescriptor descriptor = TypeDescriptor.GetProperties(connection)["SmartcardCertificate"];

            descriptor.SetValue(connection, Identifier);

            Assert.That(descriptor.GetValue(connection), Is.EqualTo(Identifier));
        }

        [Test]
        public void DescriptorIsNotReadOnly()
        {
            ConnectionInfo connection = new();

            PropertyDescriptor descriptor = TypeDescriptor.GetProperties(connection)["SmartcardCertificate"];

            Assert.That(descriptor.IsReadOnly, Is.False);
        }

        [Test]
        public void ConverterAcceptsTypedText()
        {
            ConnectionInfo connection = new();
            PropertyDescriptor descriptor = TypeDescriptor.GetProperties(connection)["SmartcardCertificate"];

            object converted = descriptor.Converter.ConvertFromString(Identifier);

            Assert.That(converted, Is.EqualTo(Identifier));
        }
    }
}
