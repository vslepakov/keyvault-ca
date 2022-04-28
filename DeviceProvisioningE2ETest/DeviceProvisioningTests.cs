using Microsoft.Azure.Devices;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.IO;
using System.Threading.Tasks;

namespace DeviceProvisioningE2ETest
{
    public class DeviceProvisioningTests
    {
        IConfiguration configuration;

        [SetUp]
        public void Setup()
        {
            configuration = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("appsettings.json", optional: true)
               .AddEnvironmentVariables()
               .Build();
        }

        [Test]
        public async Task EdgeDeviceIsProvisionedAsync()
        {
            // Arrange
            var iotHubConnectionString = configuration.GetSection("IotHubConnectionString").Value;
            var targetDevice = configuration.GetSection("EdgeDeviceName").Value;
            var registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);

            // Act
            var device = await registryManager.GetDeviceAsync(targetDevice);
            var deviceStatus = device?.Status;

            // Assert
            Assert.IsNotNull(device);
            Assert.AreEqual(deviceStatus, DeviceStatus.Enabled);
        }
    }
}