using System.Numerics;
using System.Security.Cryptography;
using MerkleTrees;
using static MerkleTrees.LeafNode;

class Program 
{
    static void Main()
    {
        // Binary Test

        /*byte[][] leafs = new byte[1 << 16][];
        for (int i = 0; i < leafs.Length; i++) 
        {
            leafs[i] = RandomNumberGenerator.GetBytes(32);
        }
        BinaryTree tree = new BinaryTree(new byte[][] { new byte[] { 1 } });
        for (int i = 0; i < 1 << 16; i++) 
        {
            tree.Add(leafs[i]);
        }
        for (int i = 0; i < 1 << 16; i++) 
        {
            tree.ExecuteProof(i + 1, leafs[i]);
        }
        Console.WriteLine(tree.ExecuteProof(14325, leafs[14324])); 
        Console.WriteLine(tree.ExecuteProof(14325, new byte[] { 1, 2, 3 }));*/


        // Sparse Test 

        /*SparseTree256 tree = new SparseTree256();
        SHA256 sha256 = SHA256.Create();
        byte[][] leafs = new byte[1 << 16][];
        for (int i = 0; i < leafs.Length; i++)
        {
            leafs[i] = RandomNumberGenerator.GetBytes(32);
        }
        for (int i = 0; i < leafs.Length; i++) 
        {
            tree.Insert(leafs[i]);
        }
        Console.WriteLine(tree.ExecuteNonMembershipProof(1 << 15));*/

        // Indexed Test

        /*SHA256 sha256 = SHA256.Create();
        IndexedTree tree = new IndexedTree();

        byte[] leaf = new byte[] { 1 };
        byte[] leaf1 = new byte[] { 2 };
        tree.Insert(leaf);
        Console.WriteLine(tree.ExecuteNonMembershipProof(sha256.ComputeHash(leaf)));
        Console.WriteLine(tree.ExecuteNonMembershipProof(sha256.ComputeHash(leaf1)));

        byte[][] leafs = new byte[1 << 16][];
        for (int i = 0; i < 1 << 16; i++)
        {
            leafs[i] = RandomNumberGenerator.GetBytes(4);
        }
        for (int i = 0; i < 1 << 16; i++) 
        {
            tree.Insert(leafs[i]);
        }
        Console.WriteLine(tree.ExecuteNonMembershipProof(sha256.ComputeHash(new byte[] { 5 })));*/

    }
}
class BinaryTree
{
    public List<byte[]> leafs;
    public List<byte[][]> eigenTrees = new List<byte[][]>();
    public byte[] root;
    SHA256 sha256 = SHA256.Create();
    public BinaryTree(byte[][] leafs)
    {
        this.leafs = leafs.ToList();
        ConstructTree();
        ComputeRoot();
    }
    public BinaryTree(List<byte[]> leafs)
    {
        this.leafs = leafs;
    }
    public void Add(byte[] leaf)
    {
        leafs.Add(leaf);
        byte[] hash = sha256.ComputeHash(leaf);
        byte[][] tree = new byte[1][] { hash };
        eigenTrees.Add(tree);
        MergeAll();
        ComputeRoot();
    }
    void MergeAll()
    {
        for (int i = 0; i < eigenTrees.Count - 1; i++)
        {
            byte[][] tree1 = eigenTrees[i];
            byte[][] tree2 = eigenTrees[i + 1];
            if (tree1.Length == tree2.Length)
            {
                byte[][] merged = MergeTrees(tree1, tree2);
                eigenTrees.RemoveAt(i); eigenTrees.RemoveAt(i);
                eigenTrees.Add(merged);
                i = -1;
                continue;
            }
        }
    }
    byte[][] MergeTrees(byte[][] tree1, byte[][] tree2)
    {
        int size = tree1.Length;
        byte[][] result = new byte[size << 1][];
        Array.Copy(tree1, 0, result, 0, size); Array.Copy(tree2, 0, result, size, size);
        byte[] concat = new byte[64];
        byte[] eigenRoot1 = tree1[(size - 1) >> 1];
        byte[] eigenRoot2 = tree2[(size - 1) >> 1];
        Array.Copy(eigenRoot1, 0, concat, 0, 32); Array.Copy(eigenRoot2, 0, concat, 32, 32);
        concat = sha256.ComputeHash(concat);
        result[((size << 1) - 1) >> 1] = concat;
        return result;
    }
    public bool ExecuteProof(int index, byte[] leaf) 
    {
        int eigenTreeIndex;
        byte[] eigenRoot = GetEigenRoot(index, leaf, out eigenTreeIndex);
        byte[][] final = new byte[eigenTrees.Count][];
        byte[] concat = new byte[64];
        for (int i = 0; i < eigenTrees.Count; i++)
        {
            if (i == eigenTreeIndex)
            {
                final[i] = eigenRoot;
            }
            else
            {
                byte[][] tree = eigenTrees[i];
                final[i] = tree[(tree.Length - 1) >> 1];
            }
        }
        byte[] initial = final[0];
        for (int i = 1; i < eigenTrees.Count; i++)
        {
            Array.Copy(initial, 0, concat, 0, 32); Array.Copy(final[i], 0, concat, 32, 32);
            initial = sha256.ComputeHash(concat);
        }
        return root.SequenceEqual(initial);
    }
    byte[] GetEigenRoot(int index, byte[] leaf, out int eigenTreeIndex) 
    {
        eigenTreeIndex = 0;
        int originalIndex = index;
        byte[] concat = new byte[64];
        byte[] branchHash = sha256.ComputeHash(leaf);
        while (index >= eigenTrees[eigenTreeIndex].Length)
        {
            index %= eigenTrees[eigenTreeIndex].Length;
            eigenTreeIndex++;
        }
        if (eigenTrees[eigenTreeIndex].Length == 1)
        {
            return branchHash;
        }
        byte[] sibLeaf = leafs[originalIndex + (1 - ((index & 1) << 1))];
        sibLeaf = sha256.ComputeHash(sibLeaf);
        if ((index & 1) == 1)
        {
            Array.Copy(sibLeaf, 0, concat, 0, 32); Array.Copy(branchHash, 0, concat, 32, 32);
        }
        else
        {
            Array.Copy(branchHash, 0, concat, 0, 32); Array.Copy(sibLeaf, 0, concat, 32, 32);
        }
        branchHash = sha256.ComputeHash(concat);
        byte[][] eigenTree = eigenTrees[eigenTreeIndex];
        int power = 30 - BitOperations.LeadingZeroCount((uint)eigenTree.Length);
        int step = 2;
        index -= index & 1;
        int targetIndex = index;
        index >>= 1;
        for (int i = 0; i < power; i++)
        {
            int direction = index & 1;
            int diff = (1 - (direction << 1)) * step;
            int hashIndex = targetIndex + diff;
            if (hashIndex < targetIndex)
            {
                Array.Copy(eigenTree[hashIndex], 0, concat, 0, 32); Array.Copy(branchHash, 0, concat, 32, 32);
            }
            else
            {
                Array.Copy(branchHash, 0, concat, 0, 32); Array.Copy(eigenTree[hashIndex], 0, concat, 32, 32);
            }
            branchHash = sha256.ComputeHash(concat);
            targetIndex = (targetIndex + hashIndex) >> 1;
            index >>= 1;
            step <<= 1;
        }
        return branchHash;
    }

    public void ComputeRoot() 
    {
        byte[] concat = new byte[64];
        byte[] mainRoot = new byte[32];
        Array.Copy(eigenTrees[0][(eigenTrees[0].Length - 1) >> 1], mainRoot, 32);
        for (int i = 1; i < eigenTrees.Count; i++)
        {
            byte[][] eigenTree = eigenTrees[i];
            byte[] eigenRoot = eigenTree[(eigenTree.Length - 1) >> 1];
            Array.Copy(mainRoot, 0, concat, 0, 32); Array.Copy(eigenRoot, 0, concat, 32, 32);
            mainRoot = sha256.ComputeHash(concat);
        }
        root = mainRoot;
    }
    public void ConstructTree() 
    {
        int length = leafs.Count;
        byte[][] leafArray = leafs.ToArray();
        int size = 1;
        int arrayIndex = length;
        while (length != 0) 
        {
            if ((length & 1) == 1) 
            {
                byte[][] subLeafs = new byte[size][];
                Array.Copy(leafArray, arrayIndex - size, subLeafs, 0, size);
                for (int i = 0; i < size; i++) 
                {
                    subLeafs[i] = sha256.ComputeHash(subLeafs[i]);
                }
                eigenTrees.Insert(0, ConstructEigenTree(subLeafs));
                arrayIndex -= size;
            }
            length >>= 1;
            size <<= 1;
        }
    }
    public byte[][] ConstructEigenTree(byte[][] leafs)
    {
        uint length = (uint)leafs.Length;
        if (length == 1) 
        {
            return leafs;
        }
        byte[] concat = new byte[64];
        int power = 31 - BitOperations.LeadingZeroCount(length);
        int step = 2;
        int start = 0;
        for (int i = 0; i < power; i++)
        {
            int previousStep = step >> 1;
            int middle = previousStep >> 1;
            for (int j = start; j < length; j += step)
            {
                byte[] b1 = leafs[j];
                byte[] b2 = leafs[j + previousStep];
                Array.Copy(b1, 0, concat, 0, 32); Array.Copy(b2, 0, concat, 32, 32);
                int targetIndex = j + middle;
                byte[] hash = sha256.ComputeHash(concat);
                Array.Copy(hash, leafs[targetIndex], 32);
            }
            step <<= 1;
            start = previousStep - 1;
        }
        return leafs;
    }
}