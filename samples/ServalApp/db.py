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
    build_id = Column("build_id", String, primary_key=True)
    engine_id = Column("engine_id", String, primary_key=True)
    email = Column("email", String)
    state = Column("state", Enum(State))
    corpus_id = Column("corpus_id", String)

    def __str__(self):
        return str(
            {
                "build_id": self.build_id,
                "engine_id": self.engine_id,
                "email": self.email,
                "state": self.state,
                "corpus_id": self.corpus_id,
            }
        )

    def __repr__(self):
        return self.__str__()


def create_db_if_not_exists():
    engine = create_engine("sqlite:///builds.db")
    metadata.create_all(bind=engine)
