i've noticed a complexity in the design. a WorkflowSpecification must be immmutable with an immutable graph, that are used to create a runtime Workflow with a runtime graph.

using our cannonical example of gathering file metadata, we have a FileMetadataReport workflow specification that has a graph with 3 tasks: DiscoverFiles, ExtractFileMetadata, and GenerateFileMetadataReport. The workflow specification is immutable and can be used to create multiple runtime workflows. Each runtime workflow has its own runtime graph that is initially the same as the specification graph, but can be mutated at runtime (e.g. by adding new tasks or dependencies).

WorkflowSpecification: MetadataReport 
    TaskSpecification: DiscoverFiles
        One runtime instance
        Input: root directory, search pattern, search options
        Output: array of file paths

    TaskSpecification: ExtractFileMetadata
        Depends on DiscoverFiles
        Runtime: Zero to many runtime instances, one per input array item
        Input: Array of file paths
        Output: Array of file metadata payloads

    TaskSpecification: SaveFileMetadata
        Depends on ExtractFileMetadata
        Runtime: Zero to many runtime instances, one per input array item
        Input: Array of file metadata payloads
        Output: Save location

    TaskSpecification: GenerateMetadataReport
        Depends on ExtractFileMetadata
        Runtime: One runtime instance
        Input: Array of file metadata payloads
        Output: Metadata report

Workflow: MetadataReport
    Task: DiscoverFiles/1
        Input: Z:/Remotefiles, *.mp4, Recursive
        Output: [Z:/Remotefiles/File1.mp4, Z:/Remotefiles/File2.mp4, Z:/Remotefiles/File3.mp4, Z:/Remotefiles/File4.mp4]

    Task: ExtractFileMetadata/1
        Depends on DiscoverFiles/1
        Input: Z:/Remotefiles/File1.mp4
        Output: {FilePath: Z:/Remotefiles/File1.mp4, FileSize: 1234567, FrameCount: 1567}
    
    Task: ExtractFileMetadata/2
        Depends on DiscoverFiles/1
        Input: Z:/Remotefiles/File2.mp4
        Output: {FilePath: Z:/Remotefiles/File2.mp4, FileSize: 1432111, FrameCount: 1789}
    
    Task: ExtractFileMetadata/3
        Depends on DiscoverFiles/1
        Input: Z:/Remotefiles/File3.mp4
        Output: {FilePath: Z:/Remotefiles/File3.mp4, FileSize: 234321, FrameCount: 567}

    Task: ExtractFileMetadata/4
        Depends on DiscoverFiles/1
        Input: Z:/Remotefiles/File3.mp4
        Output: {FilePath: Z:/Remotefiles/File4.mp4, FileSize: 897688, FrameCount: 0}

    Task: SaveFileMetadata/1
        Depends on ExtractFileMetadata/1
        Input: {FilePath: Z:/Remotefiles/File1.mp4, FileSize: 1234567, FrameCount: 1567}
        Output: Z:/Remotefiles/File1.metadata

    Task: SaveFileMetadata/2
        Depends on ExtractFileMetadata/2
        Input: {FilePath: Z:/Remotefiles/File2.mp4, FileSize: 1432111, FrameCount: 1789}
        Output: Z:/Remotefiles/File2.metadata

    Task: SaveFileMetadata/3
        Depends on ExtractFileMetadata/3
        Input: {FilePath: Z:/Remotefiles/File3.mp4, FileSize: 234321, FrameCount: 567}
        Output: Z:/Remotefiles/File3.metadata

    Task: SaveFileMetadata/4
        Depends on ExtractFileMetadata/4
        Input: {FilePath: Z:/Remotefiles/File4.mp4, FileSize: 897688, FrameCount: 0}
        Output: Z:/Remotefiles/File4.metadata

    Task: GenerateMetadataReport/1
        Depends on [ExtractFileMetadata/1, ExtractFileMetadata/2, ExtractFileMetadata/3]
        Input: [
            {FilePath: Z:/Remotefiles/File1.mp4, FileSize: 1234567, FrameCount: 1567},
            {FilePath: Z:/Remotefiles/File2.mp4, FileSize: 1432111, FrameCount: 1789}, 
            {FilePath: Z:/Remotefiles/File3.mp4, FileSize: 234321, FrameCount: 567},
            {FilePath: Z:/Remotefiles/File4.mp4, FileSize: 897688, FrameCount: 0},
            ]
        Ouput:
        [
            {FileCount: 3, TotalFileSize: 3798687, TotalFrameCount: 3923, CorruptFiles: [Z:/Remotefiles/File4.mp4]}
        ]