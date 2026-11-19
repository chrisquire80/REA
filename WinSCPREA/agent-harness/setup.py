from pathlib import Path

from setuptools import find_namespace_packages, setup


readme = Path(__file__).parent / "cli_anything" / "winscprea" / "README.md"


setup(
    name="cli-anything-winscprea",
    version="1.0.0",
    description="Agent-ready SFTP command line for WinSCPREA, built with CLI-Anything",
    long_description=readme.read_text(encoding="utf-8"),
    long_description_content_type="text/markdown",
    license_files=["CLI_ANYTHING_LICENSE.txt"],
    python_requires=">=3.10",
    packages=find_namespace_packages(include=["cli_anything.*"]),
    include_package_data=True,
    install_requires=["click>=8.1,<9", "prompt-toolkit>=3.0"],
    entry_points={
        "console_scripts": [
            "winscprea=cli_anything.winscprea.winscprea_cli:main",
        ],
    },
    classifiers=[
        "Environment :: Console",
        "Operating System :: Microsoft :: Windows",
        "Programming Language :: Python :: 3",
        "Programming Language :: Python :: 3.10",
        "Topic :: Internet :: File Transfer Protocol (FTP)",
    ],
)
