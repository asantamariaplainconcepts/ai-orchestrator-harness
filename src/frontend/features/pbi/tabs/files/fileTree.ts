/** One row of the Files tree: a directory with its children, or a file. The id is the path. */
export interface FileEntry {
  id: string;
  name: string;
  children?: FileEntry[];
}

/**
 * The flat, sorted list of paths the server answers, as the nested tree react-arborist draws:
 * directories first, then files, each by name. One pass over the paths and one sort per directory,
 * so two thousand paths are a few milliseconds.
 */
export function fileTree(paths: readonly string[]): FileEntry[] {
  const root: FileEntry = { id: "", name: "", children: [] };
  const directories = new Map<string, FileEntry>([["", root]]);

  for (const path of paths) {
    const parts = path.split("/");
    const name = parts.pop() ?? path;
    let parent = root;
    let id = "";
    for (const part of parts) {
      id = id === "" ? part : `${id}/${part}`;
      let directory = directories.get(id);
      if (!directory) {
        directory = { id, name: part, children: [] };
        directories.set(id, directory);
        parent.children?.push(directory);
      }
      parent = directory;
    }
    parent.children?.push({ id: path, name });
  }

  for (const directory of directories.values()) directory.children?.sort(byKindThenName);
  return root.children ?? [];
}

function byKindThenName(left: FileEntry, right: FileEntry): number {
  const kind = Number(!left.children) - Number(!right.children);
  return kind !== 0 ? kind : left.name.localeCompare(right.name);
}
