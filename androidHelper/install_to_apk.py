import os
import sys

import prepare.support
import helpers
import wrapper.apktool


def prepare_dir(path):
    if os.path.isdir(path):
        return

    if os.path.exists(path):
        error("%s exists but is not folder" % path)

    os.makedirs(name=path, exist_ok=True)


def file_name(path):
    return os.path.splitext(os.path.basename(path))[0]


def validate_path(path):
    return os.path.isfile(path)


def error(message):
    print("Error: %s" % message)
    exit(1)


def clean(path):
    os.remove(path)


def main():
    if len(sys.argv) != 2:
        error("usage \"py install_to_apk.py <path to apk>\"")

    apk_path = sys.argv[1]
    if not validate_path(apk_path):
        error("\"%s\" is not a file.")

    apk_path = os.path.realpath(apk_path)
    output_path = os.path.join(helpers.file_path, file_name(apk_path))

    if file_name(apk_path) == prepare.support.support_dirname:
        error("apk cannot be named %s.apk" % (file_name(apk_path)))

    prepare_dir(helpers.file_path)

    if not os.path.isdir(output_path) and not wrapper.apktool.decompile(apk_path, output_path):
        error("Failed to disassemble.")

    if not prepare.support.disassemble_apk():
        error("Failed to disassemble support apk.")

    if not prepare.support.install_java(output_path):
        error("Failed to install java code from support code.")

    if not prepare.support.install_native(output_path):
        error("Failed to install native code from support code.")

    if not prepare.support.install_permissions(output_path):
        error("Failed to install permissions")


if __name__ == '__main__':
    main()
