#!/usr/bin/env bash

set -o errexit;

project_dir="$(dirname "$(realpath "$0")")";
project_name="$(jq -r .project.restore.projectName obj/project.assets.json)";
version="$(jq -r .version assets/modinfo.json)";
# TODO: Update project version based on modinfo.json

mkdir -p "$project_dir/builds";

outfile="$project_dir/builds/$project_name-$version.zip";
tmpdir="$(mktemp -d)";

dotnet build -c Release;
cp -v "bin/Release/Mods/QueueAPI.dll"  "$tmpdir/";
cp -v "bin/Release/Mods/QueueAPI.pdb"  "$tmpdir/";
cp -v assets/* "$tmpdir/";
pushd "$tmpdir";
zip -r "$outfile" .;
popd;

printf "\nBuild saved to %s\n" "$outfile";
