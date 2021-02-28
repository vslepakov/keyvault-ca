// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Linq;
using System.Security.Cryptography.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace KeyVaultCa.Core
{
    public class KeyVaultCertFactory
    {
        public const int SerialNumberLength = 20;
        public const int DefaultKeySize = 2048;

        /// <summary>
        /// Creates a KeyVault signed certificate.
        /// </summary>
        /// <returns>The signed certificate</returns>
        public static Task<X509Certificate2> CreateSignedCertificate(
            string subjectName,
            ushort keySize,
            DateTime notBefore,
            DateTime notAfter,
            ushort hashSizeInBits,
            X509Certificate2 issuerCAKeyCert,
            RSA publicKey,
            X509SignatureGenerator generator,
            bool caCert = false,
            bool intermediateCACert = false,
            int pathLengthConstraint = 0)
        {
            if (publicKey == null)
            {
                throw new NotSupportedException("Need a public key and a CA certificate.");
            }

            if (publicKey.KeySize != keySize)
            {
                throw new NotSupportedException(string.Format("Public key size {0} does not match expected key size {1}", publicKey.KeySize, keySize));
            }

            // new serial number
            byte[] serialNumber = new byte[SerialNumberLength];
            RandomNumberGenerator.Fill(serialNumber);
            serialNumber[0] &= 0x7F;

            var subjectDN = new X500DistinguishedName(subjectName);
            var request = new CertificateRequest(subjectDN, publicKey, GetRSAHashAlgorithmName(hashSizeInBits), RSASignaturePadding.Pkcs1);

            // Basic constraints
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(
                    certificateAuthority: caCert || intermediateCACert,
                    hasPathLengthConstraint: caCert || intermediateCACert,
                    pathLengthConstraint: pathLengthConstraint,
                    true));

            // Subject Key Identifier
            var ski = new X509SubjectKeyIdentifierExtension(
                request.PublicKey,
                X509SubjectKeyIdentifierHashAlgorithm.Sha1,
                false);

            request.CertificateExtensions.Add(ski);

            // Authority Key Identifier
            if (issuerCAKeyCert != null)
            {
                request.CertificateExtensions.Add(BuildAuthorityKeyIdentifier(issuerCAKeyCert));
            }
            else
            {
                request.CertificateExtensions.Add(BuildAuthorityKeyIdentifier(subjectDN, serialNumber.Reverse().ToArray(), ski));
            }

            if (caCert || intermediateCACert)
            {
                request.CertificateExtensions.Add(
                    new X509KeyUsageExtension(
                        X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                        true));
            }
            else
            {
                // Key Usage
                X509KeyUsageFlags defaultFlags =
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.DataEncipherment |
                        X509KeyUsageFlags.NonRepudiation | X509KeyUsageFlags.KeyEncipherment;

                request.CertificateExtensions.Add(new X509KeyUsageExtension(defaultFlags, true));

                // Enhanced key usage
                request.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(
                        new OidCollection {
                        new Oid("1.3.6.1.5.5.7.3.1"),
                        new Oid("1.3.6.1.5.5.7.3.2") }, true));
            }

            if (issuerCAKeyCert != null)
            {
                if (notAfter > issuerCAKeyCert.NotAfter)
                {
                    notAfter = issuerCAKeyCert.NotAfter;
                }
                if (notBefore < issuerCAKeyCert.NotBefore)
                {
                    notBefore = issuerCAKeyCert.NotBefore;
                }
            }

            var issuerSubjectName = issuerCAKeyCert != null ? issuerCAKeyCert.SubjectName : subjectDN;
            X509Certificate2 signedCert = request.Create(
                issuerSubjectName,
                generator,
                notBefore,
                notAfter,
                serialNumber
                );

            return Task.FromResult(signedCert);
        }

        /// <summary>
        /// Get RSA public key from a CSR.
        /// </summary>
        public static RSA GetRSAPublicKey(Org.BouncyCastle.Asn1.X509.SubjectPublicKeyInfo subjectPublicKeyInfo)
        {
            Org.BouncyCastle.Crypto.AsymmetricKeyParameter asymmetricKeyParameter = Org.BouncyCastle.Security.PublicKeyFactory.CreateKey(subjectPublicKeyInfo);
            Org.BouncyCastle.Crypto.Parameters.RsaKeyParameters rsaKeyParameters = (Org.BouncyCastle.Crypto.Parameters.RsaKeyParameters)asymmetricKeyParameter;
            RSAParameters rsaKeyInfo = new RSAParameters
            {
                Modulus = rsaKeyParameters.Modulus.ToByteArrayUnsigned(),
                Exponent = rsaKeyParameters.Exponent.ToByteArrayUnsigned()
            };
            RSA rsa = RSA.Create(rsaKeyInfo);
            return rsa;
        }

        private static HashAlgorithmName GetRSAHashAlgorithmName(uint hashSizeInBits)
        {
            if (hashSizeInBits <= 160)
            {
                return HashAlgorithmName.SHA1;
            }
            else if (hashSizeInBits <= 256)
            {
                return HashAlgorithmName.SHA256;
            }
            else if (hashSizeInBits <= 384)
            {
                return HashAlgorithmName.SHA384;
            }
            else
            {
                return HashAlgorithmName.SHA512;
            }
        }

        /// <summary>
        /// Convert a hex string to a byte array.
        /// </summary>
        /// <param name="hexString">The hex string</param>
        internal static byte[] HexToByteArray(string hexString)
        {
            byte[] bytes = new byte[hexString.Length / 2];

            for (int i = 0; i < hexString.Length; i += 2)
            {
                string s = hexString.Substring(i, 2);
                bytes[i / 2] = byte.Parse(s, System.Globalization.NumberStyles.HexNumber, null);
            }

            return bytes;
        }

        /// <summary>
        /// Build the Authority Key Identifier from an Issuer CA certificate.
        /// </summary>
        /// <param name="issuerCaCertificate">The issuer CA certificate</param>
        private static X509Extension BuildAuthorityKeyIdentifier(X509Certificate2 issuerCaCertificate)
        {
            // force exception if SKI is not present
            var ski = issuerCaCertificate.Extensions.OfType<X509SubjectKeyIdentifierExtension>().Single();
            return BuildAuthorityKeyIdentifier(issuerCaCertificate.SubjectName, issuerCaCertificate.GetSerialNumber(), ski);
        }

        /// <summary>
        /// Build the X509 Authority Key extension.
        /// </summary>
        /// <param name="issuerName">The distinguished name of the issuer</param>
        /// <param name="issuerSerialNumber">The serial number of the issuer</param>
        /// <param name="ski">The subject key identifier extension to use</param>
        private static X509Extension BuildAuthorityKeyIdentifier(
            X500DistinguishedName issuerName,
            byte[] issuerSerialNumber,
            X509SubjectKeyIdentifierExtension ski
            )
        {
            using (AsnWriter writer = new AsnWriter(AsnEncodingRules.DER))
            {
                writer.PushSequence();

                if (ski != null)
                {
                    Asn1Tag keyIdTag = new Asn1Tag(TagClass.ContextSpecific, 0);
                    writer.WriteOctetString(keyIdTag, HexToByteArray(ski.SubjectKeyIdentifier));
                }

                Asn1Tag issuerNameTag = new Asn1Tag(TagClass.ContextSpecific, 1);
                writer.PushSequence(issuerNameTag);

                // Add the tag to constructed context-specific 4 (GeneralName.directoryName)
                Asn1Tag directoryNameTag = new Asn1Tag(TagClass.ContextSpecific, 4, true);
                writer.PushSetOf(directoryNameTag);
                byte[] issuerNameRaw = issuerName.RawData;
                writer.WriteEncodedValue(issuerNameRaw);
                writer.PopSetOf(directoryNameTag);
                writer.PopSequence(issuerNameTag);

                Asn1Tag issuerSerialTag = new Asn1Tag(TagClass.ContextSpecific, 2);
                System.Numerics.BigInteger issuerSerial = new System.Numerics.BigInteger(issuerSerialNumber);
                writer.WriteInteger(issuerSerialTag, issuerSerial);

                writer.PopSequence();
                return new X509Extension("2.5.29.35", writer.Encode(), false);
            }
        }
    }
    /// <summary>
    /// The X509 signature generator to sign a digest with a KeyVault key.
    /// </summary>
    public class KeyVaultSignatureGenerator : X509SignatureGenerator
    {
        private X509Certificate2 _issuerCert;
        private KeyVaultServiceClient _keyVaultServiceClient;
        private readonly string _signingKey;

        /// <summary>
        /// Create the KeyVault signature generator.
        /// </summary>
        /// <param name="keyVaultServiceClient">The KeyVault service client to use</param>
        /// <param name="signingKey">The KeyVault signing key</param>
        /// <param name="issuerCertificate">The issuer certificate used for signing</param>
        public KeyVaultSignatureGenerator(
            KeyVaultServiceClient keyVaultServiceClient,
            string signingKey,
            X509Certificate2 issuerCertificate)
        {
            _issuerCert = issuerCertificate;
            _keyVaultServiceClient = keyVaultServiceClient;
            _signingKey = signingKey;
        }

        /// <summary>
        /// Callback to sign a digest with KeyVault key.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="hashAlgorithm"></param>
        /// <returns></returns>
        public override byte[] SignData(byte[] data, HashAlgorithmName hashAlgorithm)
        {
            HashAlgorithm hash;
            if (hashAlgorithm == HashAlgorithmName.SHA256)
            {
                hash = SHA256.Create();
            }
            else if (hashAlgorithm == HashAlgorithmName.SHA384)
            {
                hash = SHA384.Create();
            }
            else if (hashAlgorithm == HashAlgorithmName.SHA512)
            {
                hash = SHA512.Create();
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(hashAlgorithm), "The hash algorithm " + hashAlgorithm.Name + " is not supported.");
            }
            var digest = hash.ComputeHash(data);
            var resultKeyVaultPkcs = _keyVaultServiceClient.SignDigestAsync(_signingKey, digest, hashAlgorithm, RSASignaturePadding.Pkcs1).GetAwaiter().GetResult();
            return resultKeyVaultPkcs;
        }

        protected override PublicKey BuildPublicKey()
        {
            return _issuerCert.PublicKey;
        }

        internal static PublicKey BuildPublicKey(RSA rsa)
        {
            if (rsa == null)
            {
                throw new ArgumentNullException(nameof(rsa));
            }
            // function is never called
            return null;
        }

        public override byte[] GetSignatureAlgorithmIdentifier(HashAlgorithmName hashAlgorithm)
        {
            byte[] oidSequence;

            if (hashAlgorithm == HashAlgorithmName.SHA256)
            {
                //const string RsaPkcs1Sha256 = "1.2.840.113549.1.1.11";
                oidSequence = new byte[] { 48, 13, 6, 9, 42, 134, 72, 134, 247, 13, 1, 1, 11, 5, 0 };
            }
            else if (hashAlgorithm == HashAlgorithmName.SHA384)
            {
                //const string RsaPkcs1Sha384 = "1.2.840.113549.1.1.12";
                oidSequence = new byte[] { 48, 13, 6, 9, 42, 134, 72, 134, 247, 13, 1, 1, 12, 5, 0 };
            }
            else if (hashAlgorithm == HashAlgorithmName.SHA512)
            {
                //const string RsaPkcs1Sha512 = "1.2.840.113549.1.1.13";
                oidSequence = new byte[] { 48, 13, 6, 9, 42, 134, 72, 134, 247, 13, 1, 1, 13, 5, 0 };
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(hashAlgorithm), "The hash algorithm " + hashAlgorithm.Name + " is not supported.");
            }
            return oidSequence;
        }
    }
}
