using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Numerics;

namespace MerkleTrees
{
    internal class SparseTree256
    {
        public Node256 root;
        public static SHA256 sha256 = SHA256.Create();
        byte[] defaultValue = new byte[] { 0 };
        public static byte[][] defaultHashes;
        public static int treeDepth = 256;

        public bool ExecuteNonMembershipProof(BigInteger index)
        {
            List<byte[]> path = GetNonMembershipPath(index);
            byte[] concat = new byte[64];
            byte[] b = new byte[32];
            Array.Copy(defaultHashes[0], 0, b, 0, 32);
            for (int i = 0; i < path.Count; i++)
            {
                int j = path.Count - i - 1;
                if ((index & 1) == 0)
                {
                    Array.Copy(b, 0, concat, 0, 32); Array.Copy(path[j], 0, concat, 32, 32);
                }
                else
                {
                    Array.Copy(path[j], 0, concat, 0, 32); Array.Copy(b, 0, concat, 32, 32);
                }
                b = sha256.ComputeHash(concat);
                index >>= 1;
            }
            return b.SequenceEqual(root.hash);
        }
        public List<byte[]> GetNonMembershipPath(BigInteger index)
        {
            List<byte[]> path = new List<byte[]>();
            BigInteger bit = (BigInteger)1 << (treeDepth - 1);
            root.GetNonMembershipPath(index, 0, path, bit);
            return path;
        }
        public bool ExecuteMembershipProof(byte[] leafHash)
        {
            BigInteger index = new BigInteger(leafHash, true);
            //index *= index.Sign;
            List<byte[]> path = GetMembershipProof(index);
            byte[] concat = new byte[64];
            byte[] b = new byte[32];
            Array.Copy(leafHash, 0, b, 0, 32);
            for (int i = 0; i < path.Count; i++)
            {
                int j = path.Count - i - 1;
                if ((index & 1) == 0)
                {
                    Array.Copy(b, 0, concat, 0, 32); Array.Copy(path[j], 0, concat, 32, 32);
                }
                else
                {
                    Array.Copy(path[j], 0, concat, 0, 32); Array.Copy(b, 0, concat, 32, 32);
                }
                b = sha256.ComputeHash(concat);
                index >>= 1;
            }
            return b.SequenceEqual(root.hash);
        }
        public List<byte[]> GetMembershipProof(BigInteger index)
        {
            List<byte[]> path = new List<byte[]>();
            BigInteger bit = (BigInteger)1 << (treeDepth - 1);
            root.GetPath(index, 0, path, bit);
            return path;
        }
        public void Insert(byte[] leaf)
        {
            BigInteger index = new BigInteger(leaf, true);
            //index *= index.Sign;
            BigInteger bit = (BigInteger) 1 << (treeDepth - 1);
            root.Insert(index, leaf, 0, bit);
        }
        public SparseTree256()
        {
            ComputeDefaultHashes();
            root = new Node256(defaultHashes[treeDepth - 1]);
        }

        public string AsDot()
        {
            return $"digraph G {{{root.AsDot()}}}";
        }

        void ComputeDefaultHashes()
        {
            byte[] concat = new byte[64];
            defaultValue = sha256.ComputeHash(defaultValue);
            defaultHashes = new byte[treeDepth + 1][];
            defaultHashes[0] = defaultValue;
            Array.Copy(defaultValue, 0, concat, 0, 32); Array.Copy(defaultValue, 0, concat, 32, 32);
            for (int i = 1; i < treeDepth + 1; i++)
            {
                byte[] hash = sha256.ComputeHash(concat);
                defaultHashes[i] = hash;
                Array.Copy(hash, 0, concat, 0, 32); Array.Copy(hash, 0, concat, 32, 32);
            }
        }
    }
    internal class Node256
    {
        public byte[] hash;
        public Node256 left = null;
        public Node256 right = null;

        public Node256(byte[] hash, Node256 left, Node256 right)
        {
            this.hash = hash;
            this.left = left;
            this.right = right;
        }
        public Node256(byte[] hash)
        {
            this.hash = hash;
        }
        public void GetNonMembershipPath(BigInteger index, int depth, List<byte[]> path, BigInteger bit)
        {
            int treeDepth = SparseTree256.treeDepth;
            if (depth == SparseTree256.treeDepth)
            {
                return;
            }
            if (right is null)
            {
                for (int i = depth; i < treeDepth; i++)
                {
                    path.Add(SparseTree256.defaultHashes[treeDepth - i - 1]);
                }
                return;
            }
            if (MoveRight(index, bit))
            {
                path.Add(left.hash);
                right.GetNonMembershipPath(index, depth + 1, path, bit >> 1);
            }
            else
            {
                path.Add(right.hash);
                left.GetNonMembershipPath(index, depth + 1, path, bit >> 1);
            }
        }
        public void GetPath(BigInteger index, int depth, List<byte[]> path, BigInteger bit)
        {
            if (depth == SparseTree256.treeDepth)
            {
                return;
            }
            if (MoveRight(index, bit))
            {
                path.Add(left.hash);
                right.GetPath(index, depth + 1, path, bit >> 1);
            }
            else
            {
                path.Add(right.hash);
                left.GetPath(index, depth + 1, path, bit >> 1);
            }
        }
        public void Insert(BigInteger index, byte[] leaf, int depth, BigInteger bit)
        {
            int treeDepth = SparseTree256.treeDepth;
            if (depth == SparseTree256.treeDepth)
            {
                hash = leaf;
                return;
            }
            if (right is null)
            {
                right = new Node256(SparseTree256.defaultHashes[treeDepth - depth - 1]);
            }
            if (left is null)
            {
                left = new Node256(SparseTree256.defaultHashes[treeDepth - depth - 1]);
            }
            if (MoveRight(index, bit))
            {
                right.Insert(index, leaf, depth + 1, bit >> 1);
            }
            else
            {
                left.Insert(index, leaf, depth + 1, bit >> 1);
            }
            byte[] concat = new byte[64];
            Array.Copy(left.hash, 0, concat, 0, 32);
            Array.Copy(right.hash, 0, concat, 32, 32);
            hash = SparseTree256.sha256.ComputeHash(concat);
        }

        public string AsDot()
        {
            if (left == null)
            {
                return "";
            }

            var self = $"{ShortHash()} -> {left.ShortHash()}\n{ShortHash()} -> {right.ShortHash()}";
            return $"{self}\n{left.AsDot()}\n{right.AsDot()}";
        }

        private string ShortHash()
        {
            return $"H_{BitConverter.ToString(hash, 0, 4).Replace("-", "")}";
        }

        public static bool MoveRight(BigInteger index, BigInteger bit)
        {
            return (index & bit) != 0;
        }
    }
}
