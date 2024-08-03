using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MerkleTrees
{
    internal class IndexedTree
    {
        public InnerNode root;
        public static SHA256 sha256 = SHA256.Create();
        public static byte[] defaultValue = new byte[32];
        public static byte[][] defaultHashes;
        public static int treeDepth = 31;
        public List<LeafNode> leafs = new List<LeafNode>();

        public IndexedTree()
        {
            ComputeDefaultHashes();
            root = new InnerNode(defaultHashes[treeDepth - 1]);
            leafs.Add(new LeafNode(defaultHashes[0], 0, new U256(0, 0, 0, 0), new U256(0, 0, 0, 0)));
            root.Insert(0, defaultHashes[1], 0, 1 << (treeDepth - 1));
        }
        public void Insert(byte[] value)
        {
            byte[] hash = sha256.ComputeHash(value);
            (LeafNode, int) proof = GetNonMembershipNode(hash);
            LeafNode nmNode = proof.Item1;
            if (!ExecuteMembershipProof(proof.Item2, nmNode.value))
            {
                return;
            }
            U256 longValue = new U256(hash);
            LeafNode leaf = new LeafNode(hash, nmNode.nextIndex, longValue, nmNode.nextValue);
            nmNode.nextValue = longValue;
            nmNode.nextIndex = leafs.Count;
            leafs.Add(leaf);
            byte[] concat = new byte[64];
            if (((leafs.Count - 1) & 1) == 1)
            {
                Array.Copy(leafs[leafs.Count - 2].value, 0, concat, 0, 32); Array.Copy(hash, 0, concat, 32, 32);
            }
            else 
            {
                Array.Copy(hash, 0, concat, 0, 32); Array.Copy(defaultValue, 0, concat, 32, 32);
            }
            root.Insert(leafs.Count - 1, sha256.ComputeHash(concat), 0, 1 << 30);
        }
        public bool ExecuteNonMembershipProof(byte[] value)
        {
            (LeafNode, int) nmNode = GetNonMembershipNode(value);
            return ExecuteMembershipProof(nmNode.Item2, nmNode.Item1.value);
        }
        public (LeafNode, int) GetNonMembershipNode(byte[] value)
        {
            (LeafNode, int) result = (new LeafNode(), -1);
            U256 thisValue = new U256(value);
            for (int i = 0; i < leafs.Count; i++)
            {
                LeafNode node = leafs[i];
                bool l = thisValue > node.longValue;
                if ((l && node.nextValue > thisValue) || (l && node.nextIndex == 0))
                {
                    result = (node, i);
                    break;
                }
            }
            return result;
        }
        public bool ExecuteMembershipProof(int index, byte[] leafHash)
        {
            if (index == -1) 
            {
                return false;
            }
            List<byte[]> path = GetMembershipProof(index);
            byte[] concat = new byte[64];
            byte[] b = new byte[32];
            Array.Copy(leafHash, 0, b, 0, 32);
            int pathLength = path.Count;
            for (int i = 0; i < pathLength; i++)
            {
                int j = pathLength - i - 1;
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
        public List<byte[]> GetMembershipProof(int index)
        {
            List<byte[]> path = new List<byte[]>();
            root.GetPath(index, 0, path, 1 << (treeDepth - 1));
            if ((index & 1) == 1)
            {
                path.Add(leafs[index - 1].value);
            }
            else
            {
                if (leafs.Count - 1 == index)
                {
                    path.Add(defaultHashes[0]);
                }
                else
                {
                    path.Add(leafs[index + 1].value);
                }
            }
            return path;
        }
        void ComputeDefaultHashes()
        {
            byte[] concat = new byte[64];
            for (int i = 0; i < 32; i++)
            {
                defaultValue[i] = 0;
            }
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
    internal class InnerNode
    {
        public byte[] hash;
        public InnerNode left = null;
        public InnerNode right = null;

        public InnerNode(byte[] hash, InnerNode left, InnerNode right)
        {
            this.hash = hash;
            this.left = left;
            this.right = right;
        }
        public InnerNode(byte[] hash)
        {
            this.hash = hash;
        }
        public void GetPath(int index, int depth, List<byte[]> path, int bit)
        {
            if (depth == IndexedTree.treeDepth - 1)
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
        public void Insert(int index, byte[] leaf, int depth, int bit)
        {
            int treeDepth = IndexedTree.treeDepth;
            byte[] concat;
            if (depth == IndexedTree.treeDepth - 1)
            {
                hash = leaf;
                return;
            }
            if (right is null)
            {
                right = new InnerNode(IndexedTree.defaultHashes[treeDepth - depth - 1]);
            }
            if (left is null)
            {
                left = new InnerNode(IndexedTree.defaultHashes[treeDepth - depth - 1]);
            }
            if (MoveRight(index, bit))
            {
                right.Insert(index, leaf, depth + 1, bit >> 1);
            }
            else
            {
                left.Insert(index, leaf, depth + 1, bit >> 1);
            }
            concat = new byte[64];
            Array.Copy(left.hash, 0, concat, 0, 32);
            Array.Copy(right.hash, 0, concat, 32, 32);
            hash = IndexedTree.sha256.ComputeHash(concat);
        }

        public static bool MoveRight(int index, int bit)
        {
            return (index & bit) != 0;
        }
    }
    internal class LeafNode
    {
        public byte[] value = IndexedTree.defaultHashes[0];
        public int nextIndex = 0;
        public U256 longValue;
        public U256 nextValue;
        public LeafNode()
        {

        }
        public LeafNode(byte[] value, int nextIndex, U256 longValue, U256 nextValue)
        {
            this.value = value;
            this.nextIndex = nextIndex;
            this.longValue = longValue;
            this.nextValue = nextValue;
        }
    }
    public struct U256
    {
        ulong v0; // Low
        ulong v1;
        ulong v2;
        ulong v3;

        public U256(byte[] bytes)
        {
            v0 = BitConverter.ToUInt64(bytes, 0);
            v1 = BitConverter.ToUInt64(bytes, 8);
            v2 = BitConverter.ToUInt64(bytes, 16);
            v3 = BitConverter.ToUInt64(bytes, 24);
        }
        public static bool Bigger(U256 lhs, U256 rhs) 
        {
            bool eq0 = lhs.v3 != rhs.v3;
            bool eq1 = lhs.v2 != rhs.v2;
            bool eq2 = lhs.v1 != rhs.v1;
            bool eq3 = lhs.v0 != rhs.v0;

            return (eq0 && lhs.v3 > rhs.v3) || (!eq0 && eq1 && lhs.v2 > rhs.v2) || (!eq1 && eq2 && lhs.v1 > rhs.v1) || (!eq2 && lhs.v0 > rhs.v0);
        }
        public U256(ulong v0, ulong v1, ulong v2, ulong v3)
        {
            this.v0 = v0;
            this.v1 = v1;
            this.v2 = v2;
            this.v3 = v3;
        }
        public static bool operator <(U256 lhs, U256 rhs)
        {
            if (lhs.v3 != rhs.v3) return lhs.v3 < rhs.v3;
            if (lhs.v2 != rhs.v2) return lhs.v2 < rhs.v2;
            if (lhs.v1 != rhs.v1) return lhs.v1 < rhs.v1;
            return lhs.v0 < rhs.v0;
        }
        public static bool operator >(U256 lhs, U256 rhs)
        {
            if (lhs.v3 != rhs.v3) return lhs.v3 > rhs.v3;
            if (lhs.v2 != rhs.v2) return lhs.v2 > rhs.v2;
            if (lhs.v1 != rhs.v1) return lhs.v1 > rhs.v1;
            return lhs.v0 > rhs.v0;
        }
    }
}
