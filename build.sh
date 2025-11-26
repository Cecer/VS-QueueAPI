#!/usr/bin/env bash

set -o errexit;

configuration="Debug";

dotnet build -c "$configuration";

project_dir="$(dirname "$(realpath "$0")")";
project_name="$(jq -r .project.restore.projectName obj/project.assets.json)";
version="$(jq -r .version assets/modinfo.json)";

# Update project file
csproj_file="$project_dir/$project_name.csproj";
for tag in Version AssemblyVersion FileVersion; do
    if grep -q "<$tag>" "$csproj_file"; then
        sed -i "s|<$tag>.*</$tag>|<$tag>$version</$tag>|" "$csproj_file";
    fi
done

printf "[Project Info]\n  Path: %s\n  Name: %s\n  Version: %s\n" "$project_dir" "$project_name" "$version";

assembled_dir="$project_dir/builds/$project_name/";
mkdir -p "$assembled_dir";
rm -Rfv "${assembled_dir:?}/"*;

cp -v "bin/$configuration/Mods/$project_name.dll"  "$assembled_dir/";
cp -v "bin/$configuration/Mods/$project_name.pdb"  "$assembled_dir/";
cp -v assets/* "$assembled_dir/";

mkdir -p "$project_dir/builds/zips";
zip_file="$project_dir/builds/zips/$project_name-$version.zip";
pushd "$assembled_dir";
zip -r "$zip_file" .;
popd;

printf "\nBuild saved to %s\n" "$zip_file";
