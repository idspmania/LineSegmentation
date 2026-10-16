An experimental Unity project exploring line segmentation algorithms for voxel-based games.

It is based on the [Amanatides–Woo](http://www.cse.yorku.ca/~amana/research/grid.pdf) event traversal algorithm. To handle edge cases, it visits every voxel touching each event point, producing a closed supercover.

![A ray (green) from the player to a target (red), with every voxel it touches marked by a magenta box](Images/supercover-example.png)
