# FileDedup

A file deduplication tool based on hard links.

## Usage

```
FileDedup <...Arguments>
```

Arguments:
| Name         | ParamCount | Alias             | Default   | Info                                                            |
| ------------ | ---------- | ----------------- | --------- | --------------------------------------------------------------- |
| `--help`     |          0 | `-h`, `-?`        |           |                                                                 |
| `--filter`   |          1 | `-f`              |           | `regex`: Filter **file** paths by given regular expression        |
| `--skip`     |          1 | `-s`              |           | `regex`: Skip **directory** paths by given regular expression     |
| `--resume`   |          1 | `-r`              |           | `string`: Start enumerating from the specific file              |
| `--min-size` |          1 | `-min`, `-m`      | `1`       | `int64`: Minimal file size                                      |
| `--dry-run`  |          1 | `-dry`, `-d`      | `true`    | `boolean`: Whether should not actually make hard links          |
| `--dup-only` |          1 | `-duponly`, `-do` | `true`    | `boolean`: Whether should show duplicated only                  |

Remaining arguments: input files/folders