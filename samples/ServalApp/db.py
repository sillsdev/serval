import enum

from sqlalchemy import Column, Enum, MetaData, String, create_engine
from sqlalchemy.orm import declarative_base


class State(enum.Enum):
    Pending = 0
    Active = 1
    Completed = 2
    Faulted = 3


metadata = MetaData()
Base = declarative_base(metadata=metadata)


class Build(Base):
    __tablename__ = "builds"

    __mapper_args__ = {"confirm_deleted_rows": False}

    build_id = Column("build_id", String, primary_key=True)
    engine_id = Column("engine_id", String, primary_key=True)
    name = Column("name", String)
    email = Column("email", String, nullable=False)
    state = Column("state", Enum(State), nullable=False)
    corpus_id = Column("corpus_id", String, nullable=False)
    client_id = Column("client_id", String, nullable=False)
    source_files = Column("source_files", String)
    target_files = Column("target_files", String)

    def __str__(self):
        return f"Build name: {self.name}\nBuild id: {self.build_id}\nClient ID: {self.client_id}\nSource files: \
{self.source_files}\nTarget files: {self.target_files}"

    def __repr__(self):
        return self.__str__()


class Param(Base):
    __tablename__ = "params"
    param_name = Column("param_name", String, primary_key=True)


def create_db_if_not_exists():
    engine = create_engine("sqlite:///builds.db")
    metadata.create_all(bind=engine)
