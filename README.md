# NTFSDuplicateLinker
Finds duplicate files in a NTFS folder structure and links them togehter using hardlinks to save diskspace

# Principle of Operation
The program looks for any files with the same name and MD5 hash.
After grouping and listing these files you can start Linking files
It will only leave one instance of a given duplicate and all the other duplicates will be linked onto one file.
