using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Secret;
using System;
using System.IO;
using System.Security.Cryptography;
using PlainToolkit.CngProtectedData;
using CngScope = PlainToolkit.CngProtectedData.DataProtectionScope;
using CapiScope = System.Security.Cryptography.DataProtectionScope;

namespace PCL.Core.Test.Utils.Secret;

[TestClass]
[DoNotParallelize] // EncryptHelper 的缓存是进程级静态
public class EncryptHelperKeyTest
{
    private static readonly byte[] Entropy = "PCL-HE Encryption Key"u8.ToArray();
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PCLHE_KeyTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        EncryptHelper.OverrideKeyFile(Path.Combine(_tempDir, "UserKey.bin"));
        EncryptHelper.ResetKeyCache();
    }

    [TestCleanup]
    public void Cleanup()
    {
        EncryptHelper.ResetKeyCache();
        EncryptHelper.OverrideKeyFile(null);
        try { Directory.Delete(_tempDir, true); } catch { /* ignore */ }
    }

    [TestMethod]
    public void Version1KeyIsMigratedToCng()
    {
        var rawKey = RandomKey();
        Write(1, ProtectedData.Protect(rawKey, Entropy, CapiScope.CurrentUser));

        CollectionAssert.AreEqual(rawKey, EncryptHelper.EncryptionKey);
        Assert.AreEqual(2u, ReadVersion(), "v1 密钥应被就地迁移为 v2");
        Assert.AreEqual(0, BrokenFiles().Length);

        EncryptHelper.ResetKeyCache(); // 迁移后的文件必须能重新读回同一把密钥
        CollectionAssert.AreEqual(rawKey, EncryptHelper.EncryptionKey);
    }

    [DataTestMethod]
    [DataRow(1u, 0)]  // 结构合法但内容损坏 / 版本未知，都应重建
    [DataRow(1u, 4)]
    [DataRow(7u, 4)]
    public void UnusableKeyFileIsQuarantinedAndRebuilt(uint version, int payloadLength)
    {
        var payload = new byte[payloadLength];
        Random.Shared.NextBytes(payload);
        Write(version, payload);

        Assert.AreEqual(32, EncryptHelper.EncryptionKey.Length);
        Assert.AreEqual(1, BrokenFiles().Length, "坏文件应被改名留存");
        Assert.AreEqual(2u, ReadVersion(), "应生成 version 2 新密钥");
    }

    [TestMethod]
    public void LockedKeyFileIsNotRotated()
    {
        Write(1, new byte[64]);
        using (new FileStream(EncryptHelper.KeyFilePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            // 瞬时占用是 IOException，必须原样抛出而不是轮换
            Assert.ThrowsExactly<IOException>(() => EncryptHelper.EncryptionKey);
        }
        Assert.IsTrue(File.Exists(EncryptHelper.KeyFilePath), "瞬时占用不应导致密钥被换掉");
        Assert.AreEqual(0, BrokenFiles().Length);
    }

    /// <summary>
    /// 早期版本写下的密钥文件用另一组 DPAPI 熵值保护。读取时必须能回退解开，
    /// 并就地用当前熵值重写——否则升级后所有已保存的账户档案都会失效。
    /// </summary>
    [TestMethod]
    public void KeyProtectedWithLegacyEntropyIsMigratedToCurrentEntropy()
    {
        var rawKey = RandomKey();
        var legacyEntropy = LegacyEntropy();
        Write(2, CngProtectedData.Protect(rawKey, legacyEntropy, CngScope.CurrentUser));

        CollectionAssert.AreEqual(rawKey, EncryptHelper.EncryptionKey, "历史熵值保护的密钥必须仍能解出");
        Assert.AreEqual(0, BrokenFiles().Length, "不应把可读的密钥当成坏文件隔离");

        EncryptHelper.ResetKeyCache();
        CollectionAssert.AreEqual(rawKey, EncryptHelper.EncryptionKey, "迁移后必须能读回同一把密钥");

        // 迁移应已就地发生：文件里的密文不能再被历史熵值解开
        var rewritten = EncryptHelper.EncryptionData.FromBytes(File.ReadAllBytes(EncryptHelper.KeyFilePath));
        Assert.ThrowsExactly<System.Security.Cryptography.CryptographicException>(() =>
            CngProtectedData.Unprotect(rewritten.Data, legacyEntropy, CngScope.CurrentUser));
    }

    /// <summary>与生产代码中保留的历史熵值保持一致，仅用于验证兼容路径。</summary>
    private static byte[] LegacyEntropy() =>
    [
        0x50, 0x43, 0x4C, 0x20, 0x43, 0x45, 0x20, 0x45, 0x6E, 0x63, 0x72,
        0x79, 0x70, 0x74, 0x69, 0x6F, 0x6E, 0x20, 0x4B, 0x65, 0x79
    ];

    private static byte[] RandomKey()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    private static void Write(uint version, byte[] protectedKey) => File.WriteAllBytes(
        EncryptHelper.KeyFilePath,
        EncryptHelper.EncryptionData.ToBytes(new EncryptHelper.EncryptionData
        {
            Version = version,
            Data = protectedKey
        }));

    private uint ReadVersion() =>
        EncryptHelper.EncryptionData.FromBytes(File.ReadAllBytes(EncryptHelper.KeyFilePath)).Version;

    private string[] BrokenFiles() =>
        Directory.GetFiles(_tempDir, "UserKey.bin.broken-*");
}